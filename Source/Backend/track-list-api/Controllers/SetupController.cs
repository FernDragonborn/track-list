using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using api.DbContext;
using api.Services.IServices;
using api.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace api.Controllers;

[ApiController]
[Route("api/setup")]
public sealed class SetupController(
	TrackListDbContext db,
	IAuthService authService,
	IWebHostEnvironment env) : ControllerBase
{
	[HttpGet("status")]
	public async Task<IActionResult> GetStatus()
	{
		var hasAnyUser = await db.Users.IgnoreQueryFilters().AnyAsync();
		return Ok(new { needsSetup = !hasAnyUser });
	}

	[EnableRateLimiting("auth")]
	[HttpPost("admin")]
	public async Task<IActionResult> CreateFirstAdmin([FromBody] SetupAdminRequest request)
	{
		if (await db.Users.IgnoreQueryFilters().AnyAsync())
			return Conflict(new { error = "Setup is already complete." });

		if (SelfHostSecurityOptions.ProductionSetupTokenRequired(env))
		{
			var expectedToken = SelfHostSecurityOptions.Get("TRACKLIST_SETUP_TOKEN");
			if (string.IsNullOrWhiteSpace(expectedToken))
				return StatusCode(StatusCodes.Status503ServiceUnavailable,
					new { error = "TRACKLIST_SETUP_TOKEN must be configured before production setup." });

			var providedToken = Request.Headers["X-Setup-Token"].FirstOrDefault() ?? request.SetupToken;
			if (!ConstantTimeEquals(expectedToken, providedToken))
				return StatusCode(StatusCodes.Status403Forbidden, new { error = "Invalid setup token." });
		}

		var validationError = Validate(request);
		if (validationError is not null)
			return BadRequest(new { error = validationError });

		var username = request.Username.Trim();
		var email = request.Email.Trim();
		var salt = BCrypt.Net.BCrypt.GenerateSalt();
		var user = new User(salt)
		{
			Username = username,
			Email = email,
			Role = UserRole.Admin
		};
		user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, user.PasswordSalt);
		user.ProfilePicUrl = $"https://api.dicebear.com/7.x/avataaars/png?seed={user.Id:N}&size=200";

		db.Users.Add(user);
		db.Playlists.Add(new Playlist
		{
			OwnerId = user.Id,
			Name = CollectionConstants.DefaultCollectionName,
			PrivacyLevel = PlaylistPrivacyLevel.Public
		});
		await db.SaveChangesAsync();

		var tokens = await authService.LoginAsync(new LoginRequest(email, username, request.Password));
		return tokens.IsSuccess
			? Ok(new { data = tokens.Value })
			: Ok(new { message = "Admin created. Please log in." });
	}

	private static string? Validate(SetupAdminRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Email))
			return "Email не може бути порожнім.";
		if (!new EmailAddressAttribute().IsValid(request.Email))
			return "Невірний формат email.";
		if (string.IsNullOrWhiteSpace(request.Username))
			return "Нікнейм не може бути порожнім.";
		if (request.Username.Length > 25)
			return "Нікнейм має бути не довше 25 символів.";
		if (request.Password != request.ConfirmPassword)
			return "Паролі не співпадають.";
		return PasswordPolicy.Validate(request.Password);
	}

	private static bool ConstantTimeEquals(string expected, string? provided)
	{
		if (provided is null) return false;
		var expectedBytes = Encoding.UTF8.GetBytes(expected);
		var providedBytes = Encoding.UTF8.GetBytes(provided);
		return expectedBytes.Length == providedBytes.Length
		       && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
	}
}

public record SetupAdminRequest(
	string Email,
	string Username,
	string Password,
	string ConfirmPassword,
	string? SetupToken);
