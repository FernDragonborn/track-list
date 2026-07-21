using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using api;
using api.DTOs;
using api.Enums;
using api.Models;
using api.Services;
using api.Repository.IReposotory;
using api.Services.IServices;
using api.Utils;
using Moq;
using Xunit;

namespace TrackListTests;

public class AuthServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userRepoMock = new Mock<IUserRepository>();
        _unitOfWorkMock.Setup(u => u.UserRepository).Returns(_userRepoMock.Object);
        _sut = new AuthService(_unitOfWorkMock.Object);
    }

    private static User CreateTestUser(string password = "Password123", UserRole role = UserRole.User)
    {
        var salt = BCrypt.Net.BCrypt.GenerateSalt();
        return new User(salt)
        {
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, salt),
            Role = role
        };
    }

    private static JwtSecurityToken DecodeToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.ReadJwtToken(token);
    }

    // ── LoginAsync ──────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokens()
    {
        var user = CreateTestUser("Password123", UserRole.Admin);
        _userRepoMock
            .Setup(r => r.GetOneAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
            .ReturnsAsync(Result.Ok(user));

        var request = new RequestTypes.LoginRequest("test@example.com", "testuser", "Password123");
        var result = await _sut.LoginAsync(request);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrEmpty(result.Value.AccessToken));
        Assert.False(string.IsNullOrEmpty(result.Value.RefreshToken));

        // Verify access token has correct claims
        var accessJwt = DecodeToken(result.Value.AccessToken);
        Assert.Equal("access", accessJwt.Claims.First(c => c.Type == "tokenType").Value);
        Assert.Equal(user.Id.ToString(), accessJwt.Claims.First(c => c.Type == "id").Value);
        Assert.Equal("Admin", accessJwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);

        // Verify refresh token has correct type
        var refreshJwt = DecodeToken(result.Value.RefreshToken);
        Assert.Equal("refresh", refreshJwt.Claims.First(c => c.Type == "tokenType").Value);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_Fails()
    {
        var user = CreateTestUser("Password123");
        _userRepoMock
            .Setup(r => r.GetOneAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
            .ReturnsAsync(Result.Ok(user));

        var request = new RequestTypes.LoginRequest("test@example.com", "testuser", "WrongPassword");
        var result = await _sut.LoginAsync(request);

        Assert.True(result.IsFailure);
        Assert.Contains("Невірний", result.Error);
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_Fails()
    {
        _userRepoMock
            .Setup(r => r.GetOneAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
            .ReturnsAsync(Result.Fail<User>("Not found"));

        var request = new RequestTypes.LoginRequest("nobody@example.com", "nobody", "Password123");
        var result = await _sut.LoginAsync(request);

        Assert.True(result.IsFailure);
        Assert.Contains("Невірний", result.Error);
    }

    [Fact]
    public async Task LoginAsync_EmptyPassword_Fails()
    {
        var request = new RequestTypes.LoginRequest("test@example.com", "testuser", "");
        var result = await _sut.LoginAsync(request);

        Assert.True(result.IsFailure);
        Assert.Contains("Невірний", result.Error);
    }

    [Fact]
    public async Task LoginAsync_EmptyEmailAndUsername_Fails()
    {
        var request = new RequestTypes.LoginRequest("", "", "Password123");
        var result = await _sut.LoginAsync(request);

        Assert.True(result.IsFailure);
        Assert.Contains("Невірний", result.Error);
    }

    // ── RenewTokenAsync ─────────────────────────────────────

    [Fact]
    public async Task RenewTokenAsync_ValidRefreshToken_ReturnsNewTokens()
    {
        var user = CreateTestUser("Password123", UserRole.Moderator);
        var refreshToken = JwtHandler.GenerateRefreshToken(user);

        _userRepoMock
            .Setup(r => r.GetOneAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
            .ReturnsAsync(Result.Ok(user));

        var result = await _sut.RenewTokenAsync(refreshToken);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrEmpty(result.Value.AccessToken));
        Assert.False(string.IsNullOrEmpty(result.Value.RefreshToken));

        // New tokens should have same user info
        var newAccess = DecodeToken(result.Value.AccessToken);
        Assert.Equal(user.Id.ToString(), newAccess.Claims.First(c => c.Type == "id").Value);
        Assert.Equal("Moderator", newAccess.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public async Task RenewTokenAsync_AccessTokenPassed_Fails()
    {
        var user = CreateTestUser();
        var accessToken = JwtHandler.GenerateAccessToken(user);

        var result = await _sut.RenewTokenAsync(accessToken);

        Assert.True(result.IsFailure);
        Assert.Contains("refresh", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenewTokenAsync_EmptyToken_Fails()
    {
        var result = await _sut.RenewTokenAsync("");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task RenewTokenAsync_UserNotInDb_Fails()
    {
        var user = CreateTestUser();
        var refreshToken = JwtHandler.GenerateRefreshToken(user);

        _userRepoMock
            .Setup(r => r.GetOneAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
            .ReturnsAsync(Result.Fail<User>("not found"));

        var result = await _sut.RenewTokenAsync(refreshToken);

        Assert.True(result.IsFailure);
        Assert.Contains("no entry", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── NotCorrectPassword ──────────────────────────────────

    [Fact]
    public void NotCorrectPassword_CorrectPassword_ReturnsFalse()
    {
        var user = CreateTestUser("Password123");

        var result = AuthService.NotCorrectPassword(user, "Password123");

        Assert.False(result);
    }

    [Fact]
    public void NotCorrectPassword_WrongPassword_ReturnsTrue()
    {
        var user = CreateTestUser("Password123");

        var result = AuthService.NotCorrectPassword(user, "WrongPassword");

        Assert.True(result);
    }
}
