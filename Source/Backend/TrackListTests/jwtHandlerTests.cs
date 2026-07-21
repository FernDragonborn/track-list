using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using api.Enums;
using api.Models;
using api.Utils;
using Xunit;

namespace TrackListTests;

public class JwtHandlerTests
{
    private static User CreateTestUser(UserRole role = UserRole.User)
    {
        var salt = BCrypt.Net.BCrypt.GenerateSalt();
        return new User(salt)
        {
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123", salt),
            Role = role
        };
    }

    private static JwtSecurityToken DecodeToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.ReadJwtToken(token);
    }

    // ── GenerateAccessToken ─────────────────────────────────

    [Fact]
    public void GenerateAccessToken_ContainsCorrectClaims()
    {
        var user = CreateTestUser(UserRole.Admin);
        var token = JwtHandler.GenerateAccessToken(user);
        var jwt = DecodeToken(token);

        Assert.Equal("access", jwt.Claims.First(c => c.Type == "tokenType").Value);
        Assert.Equal(user.Id.ToString(), jwt.Claims.First(c => c.Type == "id").Value);
        Assert.Equal(user.Role.ToString(), jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal(user.Username, jwt.Claims.First(c => c.Type == ClaimTypes.Name).Value);
    }

    [Fact]
    public void GenerateAccessToken_ExpiresIn15Minutes()
    {
        var user = CreateTestUser();
        var token = JwtHandler.GenerateAccessToken(user);
        var jwt = DecodeToken(token);

        var expiry = jwt.ValidTo;
        var expectedExpiry = DateTime.UtcNow.AddMinutes(15);

        // Allow 30-second tolerance
        Assert.InRange(expiry, expectedExpiry.AddSeconds(-30), expectedExpiry.AddSeconds(30));
    }

    // ── GenerateRefreshToken ────────────────────────────────

    [Fact]
    public void GenerateRefreshToken_ContainsCorrectClaims()
    {
        var user = CreateTestUser(UserRole.Moderator);
        var token = JwtHandler.GenerateRefreshToken(user);
        var jwt = DecodeToken(token);

        Assert.Equal("refresh", jwt.Claims.First(c => c.Type == "tokenType").Value);
        Assert.Equal(user.Id.ToString(), jwt.Claims.First(c => c.Type == "id").Value);
        Assert.Equal(user.Role.ToString(), jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal(user.Username, jwt.Claims.First(c => c.Type == ClaimTypes.Name).Value);
    }

    [Fact]
    public void GenerateRefreshToken_ExpiresIn14Days()
    {
        var user = CreateTestUser();
        var token = JwtHandler.GenerateRefreshToken(user);
        var jwt = DecodeToken(token);

        var expiry = jwt.ValidTo;
        var expectedExpiry = DateTime.UtcNow.AddDays(14);

        Assert.InRange(expiry, expectedExpiry.AddSeconds(-30), expectedExpiry.AddSeconds(30));
    }

    // ── ValidateRefreshTokenAndGetId ────────────────────────

    [Fact]
    public void ValidateRefreshTokenAndGetId_ValidToken_ReturnsGuid()
    {
        var user = CreateTestUser();
        var token = JwtHandler.GenerateRefreshToken(user);

        var result = JwtHandler.ValidateRefreshTokenAndGetId(token);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value);
    }

    [Fact]
    public void ValidateRefreshTokenAndGetId_AccessTokenPassed_Fails()
    {
        var user = CreateTestUser();
        var token = JwtHandler.GenerateAccessToken(user);

        var result = JwtHandler.ValidateRefreshTokenAndGetId(token);

        Assert.True(result.IsFailure);
        Assert.Contains("refresh", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRefreshTokenAndGetId_EmptyString_Fails()
    {
        var result = JwtHandler.ValidateRefreshTokenAndGetId("");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ValidateRefreshTokenAndGetId_TamperedToken_Fails()
    {
        var user = CreateTestUser();
        var token = JwtHandler.GenerateRefreshToken(user);

        // Tamper with signature section (keeps valid base64 structure)
        var parts = token.Split('.');
        var sigBytes = Convert.FromBase64String(parts[2].Replace('-', '+').Replace('_', '/').PadRight((parts[2].Length + 3) & ~3, '='));
        sigBytes[0] ^= 0xFF;
        parts[2] = Convert.ToBase64String(sigBytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var tampered = string.Join('.', parts);

        var result = JwtHandler.ValidateRefreshTokenAndGetId(tampered);

        Assert.True(result.IsFailure);
    }

    // ── GetIdFromToken ──────────────────────────────────────

    [Fact]
    public void GetIdFromToken_ValidAccessToken_ReturnsUserId()
    {
        var user = CreateTestUser();
        var token = JwtHandler.GenerateAccessToken(user);

        var result = JwtHandler.GetIdFromToken(token);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id.ToString(), result.Value);
    }

    [Fact]
    public void GetIdFromToken_NullOrEmpty_Fails()
    {
        var resultNull = JwtHandler.GetIdFromToken(null);
        var resultEmpty = JwtHandler.GetIdFromToken("");

        Assert.True(resultNull.IsFailure);
        Assert.True(resultEmpty.IsFailure);
    }

    // ── IsRoleHigherThan ────────────────────────────────────

    [Fact]
    public void IsRoleHigherThan_AdminVsUser_ReturnsTrue()
    {
        var user = CreateTestUser(UserRole.Admin);
        var token = JwtHandler.GenerateAccessToken(user);

        var result = JwtHandler.IsRoleHigherThan(UserRole.User, token);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public void IsRoleHigherThan_UserVsAdmin_ReturnsFalse()
    {
        var user = CreateTestUser(UserRole.User);
        var token = JwtHandler.GenerateAccessToken(user);

        var result = JwtHandler.IsRoleHigherThan(UserRole.Admin, token);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public void IsRoleHigherThan_SameRole_ReturnsFalse()
    {
        var user = CreateTestUser(UserRole.Moderator);
        var token = JwtHandler.GenerateAccessToken(user);

        var result = JwtHandler.IsRoleHigherThan(UserRole.Moderator, token);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public void IsRoleHigherThan_RefreshTokenPassed_Fails()
    {
        var user = CreateTestUser(UserRole.Admin);
        var token = JwtHandler.GenerateRefreshToken(user);

        var result = JwtHandler.IsRoleHigherThan(UserRole.User, token);

        Assert.True(result.IsFailure);
        Assert.Contains("access", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── GetUserRole ─────────────────────────────────────────

    [Fact]
    public void GetUserRole_ValidAccessToken_ReturnsRole()
    {
        var user = CreateTestUser(UserRole.Admin);
        var token = JwtHandler.GenerateAccessToken(user);

        var result = JwtHandler.GetUserRole(token);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserRole.Admin, result.Value);
    }

    [Fact]
    public void GetUserRole_RefreshToken_Fails()
    {
        var user = CreateTestUser(UserRole.User);
        var token = JwtHandler.GenerateRefreshToken(user);

        var result = JwtHandler.GetUserRole(token);

        Assert.True(result.IsFailure);
        Assert.Contains("access", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
