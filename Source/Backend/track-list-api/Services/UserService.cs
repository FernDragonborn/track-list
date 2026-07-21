using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text.Json;
using api.Identity;
using api.Services.IServices;
using api.Utils;
using AutoMapper;
using dotenv.net;
using static api.DTOs.ResponseTypes;

// ReSharper disable ClassWithVirtualMembersNeverInherited.Global

namespace api.Services;

public class UserService(IUnitOfWork unitOfWork, IMapper mapper, IHttpClientFactory? httpClientFactory = null) : IUserService
{
	private const string PasswordSymbols = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
	private const long MaxAvatarBytes = 5 * 1024 * 1024;
	private readonly IDictionary<string, string> _env = DotEnv.Read();

	public virtual async Task<Result> RegisterUserAsync(RegisterRequest dto)
	{
		RegisterRequestValidator validator = new();
		var validationResult = await validator.ValidateAsync(dto);
		if (!validationResult.IsValid)
		{
			var errorMsg = string.Join("; \n", validationResult.Errors);
			return Result.Fail<TokensResponse>(errorMsg);
		}

		if (!SelfHostSecurityOptions.PublicRegistrationEnabled())
			return Result.Fail<TokensResponse>("Публічна реєстрація вимкнена. Створіть першого адміністратора через /setup або запросіть користувача.");

		var maxUsers = SelfHostSecurityOptions.MaxUsers();
		if (maxUsers is not null)
		{
			var activeUsers = await unitOfWork.UserRepository.GetAsync();
			if (activeUsers.IsFailure)
				return Result.Fail<TokensResponse>(activeUsers.Error);
			if (activeUsers.Value.Count >= maxUsers.Value)
				return Result.Fail<TokensResponse>("Досягнуто ліміт користувачів для цього self-host інстансу.");
		}

		// Active user with same email/username → hard block
		var emailActive = await unitOfWork.UserRepository.GetOneAsync(x => x.Email == dto.Email);
		if (emailActive.IsSuccess)
			return Result.Fail<TokensResponse>("Ця email-адреса вже зайнята");

		var usernameActive = await unitOfWork.UserRepository.GetOneAsync(x => x.Username == dto.Username);
		if (usernameActive.IsSuccess)
			return Result.Fail<TokensResponse>("Цей нікнейм вже зайнятий");

		// Deleted user with same email/username — free up the slot so new account can be created
		var deletedByEmail = await unitOfWork.UserRepository.GetDeletedOneAsync(x => x.Email == dto.Email);
		if (deletedByEmail.IsSuccess)
		{
			var ghost = deletedByEmail.Value;
			ghost.Email    = $"_{ghost.Id:N}@d.d";           // 37 chars, fits MaxLength(50), unique per user
			ghost.Username = $"_{ghost.Id:N}"[..25];          // exactly 25 chars, fits MaxLength(25)
			await unitOfWork.UserRepository.Update(ghost);
		}

		var deletedByUsername = await unitOfWork.UserRepository.GetDeletedOneAsync(x => x.Username == dto.Username);
		if (deletedByUsername.IsSuccess)
		{
			var ghost = deletedByUsername.Value;
			ghost.Email    = $"_{ghost.Id:N}@d.d";
			ghost.Username = $"_{ghost.Id:N}"[..25];
			await unitOfWork.UserRepository.Update(ghost);
		}

		User user = new(BCrypt.Net.BCrypt.GenerateSalt())
		{
			Email = dto.Email,
			Username = dto.Username,
			Role = UserRole.User,
		};
		user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, user.PasswordSalt);
		// Auto-assign a DiceBear avatar so new accounts aren't blank. Seed by userId (stable across renames).
		user.ProfilePicUrl = $"https://api.dicebear.com/7.x/avataaars/png?seed={user.Id:N}&size=200";

		var res = await unitOfWork.UserRepository.AddAsync(user);
		if (res.IsFailure) return Result.Fail<TokensResponse>(res.Error);

		await unitOfWork.PlaylistRepository.AddAsync(new Playlist
		{
			OwnerId = res.Value.Id,
			Name = CollectionConstants.DefaultCollectionName,
			PrivacyLevel = PlaylistPrivacyLevel.Public
		});

		await unitOfWork.SaveAsync();
		return Result.Ok();
	}

	public async Task<Result<UserDto>> GetUserByUsernameAsync(string targetUsername, string? userId)
	{
		var fetchTargetRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Username == targetUsername, "Followers,Following");
		if (fetchTargetRes.IsFailure)
			return Result.Fail<UserDto>($"User @\'{targetUsername}\' was not found");

		var dto = mapper.Map<UserDto>(fetchTargetRes.Value);
		dto.FollowersCount = fetchTargetRes.Value.Followers.Count;
		dto.FollowingCount = fetchTargetRes.Value.Following.Count;

		if (!string.IsNullOrEmpty(userId))
		{
			var userGuidParseRes = GuidParser.TryParseGuid(userId);
			if (userGuidParseRes.IsSuccess)
			{
				var fetchFollowRes = await unitOfWork.FollowRepository.GetOneAsync(f =>
					f.FollowerId == userGuidParseRes.Value
					&& f.FollowingId == fetchTargetRes.Value.Id);

				if (fetchFollowRes.IsSuccess)
					dto.IsFollowing = true;
			}
		}

		return Result.Ok(dto);
	}

	public async Task<Result<List<UserDto>>> GetFollowersAsync(string username)
	{
		var userRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Username == username);
		if (userRes.IsFailure)
			return Result.Fail<List<UserDto>>($"User '{username}' was not found.");

		var followsRes = await unitOfWork.FollowRepository.GetAsync(
			f => f.FollowingId == userRes.Value.Id, "Follower");
		if (followsRes.IsFailure)
			return Result.Fail<List<UserDto>>("Error fetching followers.");

		var dtos = followsRes.Value
			.Where(f => f.Follower != null)
			.Select(f => mapper.Map<UserDto>(f.Follower!))
			.ToList();
		return Result.Ok(dtos);
	}

	public async Task<Result<List<UserDto>>> GetFollowingAsync(string username)
	{
		var userRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Username == username);
		if (userRes.IsFailure)
			return Result.Fail<List<UserDto>>($"User '{username}' was not found.");

		var followsRes = await unitOfWork.FollowRepository.GetAsync(
			f => f.FollowerId == userRes.Value.Id,
			"Following,Following.Reviews,Following.Collections,Following.Followers");
		if (followsRes.IsFailure)
			return Result.Fail<List<UserDto>>("Error fetching following.");

		var dtos = followsRes.Value
			.Where(f => f.Following != null)
			.Select(f =>
			{
				var u = f.Following!;
				return new UserDto
				{
					Username       = u.Username,
					DisplayName    = u.DisplayName,
					Bio            = u.Bio,
					ProfilePicUrl  = u.ProfilePicUrl,
					FollowersCount = u.Followers?.Count ?? 0,
					FollowingCount = u.Following?.Count ?? 0,
					IsFollowing    = true,
					MemberSinceYear = u.CreatedAt.Year,
					ReviewsCount   = u.Reviews?.Count ?? 0,
					ListsCount     = u.Collections?.Count ?? 0,
				};
			})
			.ToList();
		return Result.Ok(dtos);
	}

	[SuppressMessage("Performance", "CA1862:Use the \'StringComparison\' method overloads to perform case-insensitive string comparisons")]
	public async Task<Result<PagedResponse<UserDto>>> GetUsersPagedAsync(UserFilterDto filterDto)
	{
		Expression<Func<User, bool>>? filter = null;

		if (!string.IsNullOrWhiteSpace(filterDto.SearchTerm))
		{
			var term = filterDto.SearchTerm.ToLower().Trim();
			filter = u => u.Username.ToLower().Contains(term)
			              || u.Email.ToLower().Contains(term);
		}

		Func<IQueryable<User>, IOrderedQueryable<User>> orderBy = (filterDto.SortBy?.ToLower(), filterDto.SortDesc) switch
		{
			("username", true)  => q => q.OrderByDescending(u => u.Username),
			("username", false) => q => q.OrderBy(u => u.Username),
			("role", true)      => q => q.OrderByDescending(u => u.Role),
			("role", false)     => q => q.OrderBy(u => u.Role),
			(_,        false)   => q => q.OrderBy(u => u.CreatedAt),
			_                   => q => q.OrderByDescending(u => u.CreatedAt),
		};

		var pagedUsersResult = await unitOfWork.UserRepository.GetPagedAsync(
			filter,
			orderBy,
			filterDto.PageNumber,
			filterDto.PageSize
		);

		if (pagedUsersResult.IsFailure)
			return Result.Fail<PagedResponse<UserDto>>(pagedUsersResult.Error);

		var (users, totalCount) = pagedUsersResult.Value;
		var userDtos = mapper.Map<List<UserDto>>(users);

		// Clear entity-type nav props — they cause Newtonsoft serialization failure in this context
		foreach (var dto in userDtos)
		{
			dto.Reviews = [];
			dto.Following = [];
			dto.Followers = [];
			dto.Collections = [];
		}

		var resultDto = new PagedResponse<UserDto>(
			userDtos,
			totalCount,
			filterDto.PageNumber,
			filterDto.PageSize
		);

		return Result.Ok(resultDto);
	}

	/// <summary>
	///     Saves uploaded avatar file to flat upload dir and updates User.ProfilePicUrl
	///     to the API-relative route ("/api/profiles/avatar/{filename}").
	///     File served by <see cref="GetAvatarAsync"/> from {FILE_STORAGE_PATH}/{filename}.
	/// </summary>
	public async Task<Result> RedactProfilePictureAsync(ProfilePictureDto pictureDto)
	{
		if (pictureDto.ProfilePic is null)
			return Result.Fail("File for ProfilePic cannot be null");

		var userFetchRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Email == pictureDto.Email);
		if (userFetchRes.IsFailure)
			return Result.Fail($"User {pictureDto.Email} was not found");

		// Flat upload dir — files served by GetAvatarAsync from {FILE_STORAGE_PATH}/{filename}
		var path = StaticDetails.UserProfileImagePath;

		// Викликаємо внутрішній метод збереження
		return await SaveAvatarAsync(pictureDto.ProfilePic, userFetchRes.Value.Id, path);
	}

	public virtual async Task<Result<string>> SaveAvatarAsync(IFormFile file, Guid userGuidId, string fileStoragePath)
	{
		if (string.IsNullOrEmpty(fileStoragePath)) return Result.Fail<string>("File storage path is not set.");

		// Ця перевірка трохи надлишкова, якщо ми викликаємо з RedactProfilePictureAsync, але не завадить
		var userFetchRes = await unitOfWork.UserRepository.GetAsync(u => u.Id == userGuidId);
		if (userFetchRes.IsFailure) return Result.Fail<string>("User not found.");

		var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), fileStoragePath);

		if (!Directory.Exists(uploadPath))
			Directory.CreateDirectory(uploadPath);

		string[] allowedExtensions = [".jpg", ".jpeg", ".png"];
		var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

		if (!allowedExtensions.Contains(extension)) return Result.Fail<string>("Invalid file type.");
		if (file.Length <= 0 || file.Length > MaxAvatarBytes)
			return Result.Fail<string>("Avatar file must be between 1 byte and 5 MB.");

		await using (var validationStream = file.OpenReadStream())
		{
			var header = new byte[8];
			var read = await validationStream.ReadAsync(header.AsMemory(0, header.Length));
			if (!HasAllowedImageMagicBytes(extension, header, read))
				return Result.Fail<string>("Avatar content does not match JPG/PNG signature.");
		}

		var fileName = Guid.NewGuid() + extension;
		var filePath = Path.Combine(uploadPath, fileName);

		await using (var input = file.OpenReadStream())
		await using (var stream = new FileStream(filePath, FileMode.CreateNew))
		{
			await input.CopyToAsync(stream);
		}

		var userImage = new UserImage(userGuidId, fileName, DateTime.UtcNow);

		var entityRes = await unitOfWork.UserImageRepository.AddAsync(userImage);
		if (entityRes.IsFailure) return Result.Fail<string>(entityRes.Error);

		// Update user's profile pic URL
		var userForPicUpdate = await unitOfWork.UserRepository.GetOneAsync(u => u.Id == userGuidId);
		if (userForPicUpdate.IsSuccess)
		{
			userForPicUpdate.Value.ProfilePicUrl = $"/api/profiles/avatar/{fileName}";
			await unitOfWork.UserRepository.Update(userForPicUpdate.Value);
		}

		await unitOfWork.SaveAsync();
		return Result.Ok(entityRes.Value.FileName);
	}

	public async Task<Result> DeleteAvatarAsync(Guid userId)
	{
		var userFetchRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Id == userId);
		if (userFetchRes.IsFailure)
			return Result.Fail("User not found");

		var imageRes = await unitOfWork.UserImageRepository.GetAsync(img => img.UserId == userId);
		if (imageRes.IsFailure || imageRes.Value.Count == 0)
			return Result.Fail("User does not have an avatar");

		var latestImage = imageRes.Value.OrderByDescending(i => i.UploadedAt).First();

		// Delete file from disk
		var filePath = Path.Combine(Directory.GetCurrentDirectory(), StaticDetails.UserProfileImagePath, latestImage.FileName);
		if (File.Exists(filePath))
			File.Delete(filePath);

		// Remove DB record
		await unitOfWork.UserImageRepository.Remove(latestImage);

		// Clear profile pic URL
		var user = userFetchRes.Value;
		user.ProfilePicUrl = null;
		await unitOfWork.UserRepository.Update(user);

		await unitOfWork.SaveAsync();
		return Result.Ok();
	}

	public async Task<Result> UpdateUserAsync(UserDto userDto, string? currentPrincipalEmail, bool isAdmin)
	{
		// Look up by current JWT identity (username stored in ClaimTypes.Name), not the submitted username.
		// This allows username changes — the old name is used to find the record.
		var userFetchRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Username == currentPrincipalEmail);
		if (userFetchRes.IsFailure)
		{
			// Admin path: fall back to submitted username if JWT identity not found
			if (!isAdmin)
				return Result.Fail($"User \'{currentPrincipalEmail}\' was not found.");
			userFetchRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Username == userDto.Username);
			if (userFetchRes.IsFailure)
				return Result.Fail($"User \'{userDto.Username}\' was not found.");
		}

		var isNotSameUser = currentPrincipalEmail != userFetchRes.Value.Username;
		if (isNotSameUser && !isAdmin)
			return Result.Fail("Недостатньо прав для редагування цього профілю.");

		if (userFetchRes.Value.Role != userDto.Role)
			return Result.Fail("Зміна ролі неможлива через цей маршрут.");

		// Check email uniqueness if it is being changed
		if (!string.IsNullOrWhiteSpace(userDto.Email) &&
		    !string.Equals(userDto.Email, userFetchRes.Value.Email, StringComparison.OrdinalIgnoreCase))
		{
			var emailTaken = await unitOfWork.UserRepository.GetOneAsync(u => u.Email == userDto.Email);
			if (emailTaken.IsSuccess)
				return Result.Fail("Цей email вже використовується.");
		}

		// Check username uniqueness if it is being changed
		if (!string.IsNullOrWhiteSpace(userDto.Username) &&
		    !string.Equals(userDto.Username, userFetchRes.Value.Username, StringComparison.OrdinalIgnoreCase))
		{
			var usernameTaken = await unitOfWork.UserRepository.GetOneAsync(u => u.Username == userDto.Username);
			if (usernameTaken.IsSuccess)
				return Result.Fail("Цей нікнейм вже зайнятий.");
		}

		var userToUpdate = userFetchRes.Value;

		// Validate avatar URL: must be either empty (clear), internal route, or http(s) URL pointing to JPG/PNG.
		if (!string.IsNullOrWhiteSpace(userDto.ProfilePicUrl))
		{
			var url = userDto.ProfilePicUrl.Trim();
			var isInternal = url.StartsWith("/", StringComparison.Ordinal)
			                 && !url.StartsWith("//", StringComparison.Ordinal)
			                 && !url.Contains('\\');
			if (!isInternal)
			{
				// Must parse as absolute http/https URL
				if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)
					|| (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
				{
					return Result.Fail("Невірний URL аватарки. Має бути http(s)://...");
				}
				var validRes = await ValidateExternalImageUrlAsync(url);
				if (validRes.IsFailure)
					return validRes;
			}
		}

		// Only map safe, user-editable fields — AutoMapper would overwrite
		// Id, timestamps, password hash/salt, and navigation properties.
		userToUpdate.Username = userDto.Username ?? userToUpdate.Username;
		userToUpdate.Email = userDto.Email ?? userToUpdate.Email;
		userToUpdate.DisplayName = userDto.DisplayName ?? userToUpdate.DisplayName;
		userToUpdate.Bio = userDto.Bio;
		userToUpdate.ProfilePicUrl = userDto.ProfilePicUrl;
		userToUpdate.Country = userDto.Country ?? userToUpdate.Country;
		userToUpdate.Gender = userDto.Gender;

		await unitOfWork.UserRepository.Update(userToUpdate);
		await unitOfWork.SaveAsync();
		return Result.Ok();
	}

	public async Task<Result> UpdateUserRoleAsync(UpdateRoleRequest updateRoleRequest, string changerEmail)
	{
		if (string.IsNullOrWhiteSpace(updateRoleRequest.TargetUsername))
			return Result.Fail("Target`s username was null, empty or whitespace");
		
		if (!Enum.IsDefined(updateRoleRequest.NewRole))
			return Result.Fail($"Role with id \'{(int)updateRoleRequest.NewRole}\' does not exist in the system.");
		
		var userFetchRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Username == updateRoleRequest.TargetUsername);
		if (userFetchRes.IsFailure)
			return Result.Fail($"User with email \'{updateRoleRequest.TargetUsername}\' was not found.");

		if (userFetchRes.Value.Username.Equals(changerEmail, StringComparison.OrdinalIgnoreCase)
		    || userFetchRes.Value.Email.Equals(changerEmail, StringComparison.OrdinalIgnoreCase))
			return Result.Fail("You cannot change your own role. (Self-destruct protection).");
		
		if (userFetchRes.Value.Role.Equals(updateRoleRequest.NewRole))
			return Result.Ok();
		
		var userToUpdate = userFetchRes.Value;
		userToUpdate.Role = updateRoleRequest.NewRole;
		userToUpdate.TokenVersion++;

		await unitOfWork.UserRepository.Update(userToUpdate);
		await unitOfWork.SaveAsync();
		return Result.Ok();
	}
	
	public async Task<Result> DeleteUserByUsernameAsync(string username)
	{
		var userFetchRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Username == username);
		if (userFetchRes.IsFailure)
			return Result.Fail($"User {username} was not found");

		await CleanupFollowsAsync(userFetchRes.Value.Id);
		await unitOfWork.UserRepository.Remove(userFetchRes.Value);
		await unitOfWork.SaveAsync();
		return Result.Ok();
	}

	public async Task<Result> FollowUserAsync(string targetUsername, string currentEmail)
	{
		var userToFollowFetchRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Username == targetUsername);
		if (userToFollowFetchRes.IsFailure)
			return Result.Fail($"User {targetUsername} was not found");

		var currentUserFetchRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Username == currentEmail);
		if (currentUserFetchRes.IsFailure)
			return Result.Fail("Username in JWT was not found in DB");

		if (currentUserFetchRes.Value.Id == userToFollowFetchRes.Value.Id)
			return Result.Fail("You cannot follow yourself");

		var existingFollowRes = await unitOfWork.FollowRepository.GetExistingAsync(
			currentUserFetchRes.Value.Id, userToFollowFetchRes.Value.Id);

		if (existingFollowRes.IsSuccess)
		{
			if (existingFollowRes.Value.DeletedAt == null)
				return Result.Ok();

			existingFollowRes.Value.DeletedAt = null;
			await unitOfWork.FollowRepository.Update(existingFollowRes.Value);
			await unitOfWork.SaveAsync();
			return Result.Ok();
		}

		var follow = new Follow
		{
			FollowerId = currentUserFetchRes.Value.Id,
			FollowingId = userToFollowFetchRes.Value.Id,
		};
		await unitOfWork.FollowRepository.AddAsync(follow);
		await unitOfWork.SaveAsync();
		return Result.Ok();
	}

	public async Task<Result> UnfollowUserAsync(string targetUsername, string currentEmail)
	{
		var userToUnfollowFetchRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Username == targetUsername);
		if (userToUnfollowFetchRes.IsFailure)
			return Result.Fail($"User {targetUsername} was not found");

		var currentUserFetchRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Username == currentEmail);
		if (currentUserFetchRes.IsFailure)
			return Result.Fail("Username in JWT was not found in DB");

		var fetchFollowRes = await unitOfWork.FollowRepository.GetOneAsync(f =>
			f.FollowerId == currentUserFetchRes.Value.Id 
			&& f.FollowingId == userToUnfollowFetchRes.Value.Id);

		if (fetchFollowRes.IsFailure)
			//Результат «не бути підписаним» досягнутий тим, що підписки не було
			return Result.Ok();

		await unitOfWork.FollowRepository.Remove(fetchFollowRes.Value);
		await unitOfWork.SaveAsync();
		return Result.Ok();
	}
	
	/// <returns>response dto with JWT pair</returns>
	public virtual async Task<Result<UserFileResponse>> GetAvatarAsync(string fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
			return Result.Fail<UserFileResponse>("Ім'я файлу не може бути порожнім.");

		string[] allowedExtensions = [".jpg", ".jpeg", ".png"];
		var extension = Path.GetExtension(fileName).ToLowerInvariant();

		var notAllowedExtension = !allowedExtensions.Contains(extension);
		if (notAllowedExtension)
			return Result.Fail<UserFileResponse>("Invalid file type.");

		// Запобігання атакам шляхового обходу
		if (fileName.Contains(".."))
			return Result.Fail<UserFileResponse>("Невірний формат імені файлу.");

		var filePath = Path.Combine(_env["FILE_STORAGE_PATH"], fileName);

		if (!File.Exists(filePath))
			return Result.Fail<UserFileResponse>("Файл не знайдено.");

		// Визначаємо MIME-тип файлу
		var extension1 = Path.GetExtension(filePath).ToLowerInvariant();
		var contentType = extension1 switch
		{
			".jpg" or ".jpeg" => "image/jpeg",
			".png" => "image/png",
			".gif" => "image/gif",
			".bmp" => "image/bmp",
			".webp" => "image/webp",
			_ => "application/octet-stream",
		};

		var bytes = await File.ReadAllBytesAsync(filePath);

		return Result.Ok(new UserFileResponse(bytes, contentType));
	}

	public virtual async Task<Result<string>> ResetPasswordAsync(string userId)
	{
		var isSuccess = Guid.TryParse(userId, out var guid);
		if (!isSuccess) return Result.Fail<string>("Id is not a Guid formatted");

		var userFetchRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Id == guid);
		if (userFetchRes.IsFailure) return Result.Fail<string>($"User with ID \"{userId}\" doesn't exist");

		userFetchRes.Value.PasswordSalt = BCrypt.Net.BCrypt.GenerateSalt();
		var generatedPassword = RandomNumberGenerator.GetString(
			PasswordSymbols,
			10);
		userFetchRes.Value.PasswordHash = BCrypt.Net.BCrypt.HashPassword(generatedPassword, userFetchRes.Value.PasswordSalt);
		userFetchRes.Value.TokenVersion++;

		await unitOfWork.UserRepository.Update(userFetchRes.Value);
		await unitOfWork.SaveAsync();

		return Result.Ok(generatedPassword);
	}

	public virtual async Task<Result> UpdatePasswordAsync(string id, string newPassword, string currentPassword)
	{
		if (string.IsNullOrEmpty(id)) return Result.Fail($"{nameof(id)} was null or empty");

		var isSuccess = Guid.TryParse(id, out var guid);
		if (!isSuccess) return Result.Fail("Id is not a Guid formatted");

		var userFetchRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Id == guid);
		if (userFetchRes.IsFailure) return Result.Fail($"User with ID \"{id}\" doesn't exist");
		if (AuthService.NotCorrectPassword(userFetchRes.Value, currentPassword))
			return Result.Fail("Невірний поточний пароль.");

		var passwordError = PasswordPolicy.Validate(newPassword);
		if (passwordError is not null)
			return Result.Fail(passwordError);

		userFetchRes.Value.PasswordSalt = BCrypt.Net.BCrypt.GenerateSalt();
		userFetchRes.Value.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, userFetchRes.Value.PasswordSalt);
		userFetchRes.Value.TokenVersion++;

		await unitOfWork.UserRepository.Update(userFetchRes.Value);
		await unitOfWork.SaveAsync();

		return Result.Ok();
	}

	public virtual async Task<Result> DeleteAnyUserAsync(string userId)
	{
		var resGuid = GuidParser.TryParseGuid(userId);
		if (!resGuid.IsSuccess) return Result.Fail(resGuid.Error);

		var userToDeleteFetchRes = await unitOfWork.UserRepository.GetOneAsync(u => u.Id == resGuid.Value);
		if (userToDeleteFetchRes.IsFailure) return Result.Fail($"User with Id: \"{userId}\" doesn't exist");

		if (userToDeleteFetchRes.Value.Role == IdentityData.ClaimAdmin) return Result.Fail("can't delete admin");

		await CleanupFollowsAsync(resGuid.Value);
		await unitOfWork.UserRepository.Remove(userToDeleteFetchRes.Value);
		await unitOfWork.SaveAsync();

		return Result.Ok();
	}

	private async Task CleanupFollowsAsync(Guid userId)
	{
		var inbound = await unitOfWork.FollowRepository.GetAsync(f => f.FollowingId == userId);
		if (inbound.IsSuccess)
			foreach (var f in inbound.Value)
				await unitOfWork.FollowRepository.Remove(f);

		var outbound = await unitOfWork.FollowRepository.GetAsync(f => f.FollowerId == userId);
		if (outbound.IsSuccess)
			foreach (var f in outbound.Value)
				await unitOfWork.FollowRepository.Remove(f);
	}

	/// <summary>
	///     HEAD-checks an external image URL with SSRF protection:
	///     - Rejects private/loopback/link-local IPs (incl. AWS metadata 169.254/16)
	///     - Disables redirect following (else attacker could 301 public → private)
	///     - Requires 2xx response and Content-Type image/jpeg or image/png
	///     If HttpClientFactory not registered (tests), skips check.
	/// </summary>
	private async Task<Result> ValidateExternalImageUrlAsync(string url)
	{
		if (httpClientFactory is null) return Result.Ok();

		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
			return Result.Fail("Невірний формат URL аватарки.");

		// SSRF: resolve hostname → block private/loopback/link-local IPs
		IPAddress[] addrs;
		try { addrs = await Dns.GetHostAddressesAsync(uri.Host); }
		catch { return Result.Fail("Не вдалося перевірити хост URL аватарки."); }

		if (addrs.Length == 0 || addrs.Any(IsBlockedIp))
			return Result.Fail("URL вказує на приватну/локальну адресу — заборонено.");

		try
		{
			using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
			using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
			using var req = new HttpRequestMessage(HttpMethod.Head, uri);
			using var resp = await client.SendAsync(req);

			// Block redirects: attacker may redirect public → private
			if ((int)resp.StatusCode >= 300 && (int)resp.StatusCode < 400)
				return Result.Fail("URL переадресовує — заборонено.");
			if (!resp.IsSuccessStatusCode)
				return Result.Fail("URL аватарки не повертає успішну відповідь.");

			var contentType = resp.Content.Headers.ContentType?.MediaType ?? "";
			var allowed = new[] { "image/jpeg", "image/jpg", "image/png" };
			if (!allowed.Contains(contentType, StringComparer.OrdinalIgnoreCase))
				return Result.Fail($"URL не вказує на зображення JPG/PNG (Content-Type: {contentType}).");

			if (resp.Content.Headers.ContentLength is > MaxAvatarBytes)
				return Result.Fail("Зображення аватарки завелике.");

			return Result.Ok();
		}
		catch (Exception ex)
		{
			return Result.Fail($"Не вдалося перевірити URL аватарки: {ex.Message}");
		}
	}

	private static bool IsBlockedIp(IPAddress ip)
	{
		if (IPAddress.IsLoopback(ip)) return true;
		if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal) return true;
		var bytes = ip.GetAddressBytes();
		if (ip.AddressFamily == AddressFamily.InterNetwork)
		{
			return bytes[0] switch
			{
				10 => true,
				127 => true,
				169 when bytes[1] == 254 => true,
				172 when bytes[1] >= 16 && bytes[1] <= 31 => true,
				192 when bytes[1] == 168 => true,
				0 => true,
				_ => false,
			};
		}
		if (ip.AddressFamily == AddressFamily.InterNetworkV6)
		{
			return bytes[0] == 0xfc || bytes[0] == 0xfd;
		}
		return false;
	}

	private static bool HasAllowedImageMagicBytes(string extension, byte[] header, int read)
	{
		if (extension is ".jpg" or ".jpeg")
			return read >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff;

		return extension == ".png"
		       && read >= 8
		       && header[0] == 0x89
		       && header[1] == 0x50
		       && header[2] == 0x4e
		       && header[3] == 0x47
		       && header[4] == 0x0d
		       && header[5] == 0x0a
		       && header[6] == 0x1a
		       && header[7] == 0x0a;
	}
}
