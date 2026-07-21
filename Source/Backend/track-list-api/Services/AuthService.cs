using api.Services.IServices;
using api.Utils;

// ReSharper disable ClassWithVirtualMembersNeverInherited.Global

namespace api.Services;

public class AuthService(IUnitOfWork unitOfWork) : IAuthService
{
	private const string WrongLoginOrPasswordStr = "Невірний нікнейм/email або пароль.";
	public virtual async Task<Result<ResponseTypes.TokensResponse>> LoginAsync(LoginRequest dto)
	{
		if (string.IsNullOrEmpty(dto.Password)
		    || string.IsNullOrEmpty(dto.Email)
		    && string.IsNullOrEmpty(dto.Username))
			return Result.Fail<ResponseTypes.TokensResponse>(WrongLoginOrPasswordStr);

		var userFetchRes = await unitOfWork.UserRepository.GetOneAsync(x => x.Email.Equals(dto.Email) || x.Username.Equals(dto.Username));

		if (userFetchRes.IsFailure
		    || NotCorrectPassword(userFetchRes.Value, dto.Password))
			return Result.Fail<ResponseTypes.TokensResponse>(WrongLoginOrPasswordStr);

		ResponseTypes.TokensResponse response = new(
			JwtHandler.GenerateAccessToken(userFetchRes.Value),
			JwtHandler.GenerateRefreshToken(userFetchRes.Value)
		);

		return Result.Ok(response);
	}

	public virtual async Task<Result<ResponseTypes.TokensResponse>> RenewTokenAsync(string jwtToken)
	{
		if (string.IsNullOrEmpty(jwtToken))
			return Result.Fail<ResponseTypes.TokensResponse>($"{nameof(jwtToken)} was null or empty.");

		var sessionRes = JwtHandler.ValidateRefreshTokenAndGetSession(jwtToken);
		if (sessionRes.IsFailure) return Result.Fail<ResponseTypes.TokensResponse>(sessionRes.Error);

		var userRes = await unitOfWork.UserRepository.GetOneAsync(x => x.Id == sessionRes.Value.UserId);
		if (userRes.IsFailure) return Result.Fail<ResponseTypes.TokensResponse>($"no entry of {nameof(User)} with provided id found in database.");
		if (userRes.Value.TokenVersion != sessionRes.Value.TokenVersion)
			return Result.Fail<ResponseTypes.TokensResponse>("Refresh token has been revoked.");

		userRes.Value.TokenVersion++;
		await unitOfWork.UserRepository.Update(userRes.Value);
		await unitOfWork.SaveAsync();

		ResponseTypes.TokensResponse response = new(
			JwtHandler.GenerateAccessToken(userRes.Value),
			JwtHandler.GenerateRefreshToken(userRes.Value)
		);

		return Result.Ok(response);
	}

	public virtual async Task<Result> LogoutAsync(string userId)
	{
		if (!Guid.TryParse(userId, out var guid))
			return Result.Fail("User id is not a valid Guid.");

		var userRes = await unitOfWork.UserRepository.GetOneAsync(x => x.Id == guid);
		if (userRes.IsFailure)
			return Result.Fail($"no entry of {nameof(User)} with provided id found in database.");

		userRes.Value.TokenVersion++;
		await unitOfWork.UserRepository.Update(userRes.Value);
		await unitOfWork.SaveAsync();
		return Result.Ok();
	}

	public static bool NotCorrectPassword(User user, string password) =>
		!user.PasswordHash
			.Equals(BCrypt.Net.BCrypt
				.HashPassword(password, user.PasswordSalt)
			);
}
