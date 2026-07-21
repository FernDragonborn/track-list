using System.Security.Claims;
using api;
using api.Controllers;
using api.DTOs;
using api.Enums;
using api.Models;
using api.Repository.IReposotory;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

// ReSharper disable Reqnroll.MethodNameMismatchPattern

namespace TrackListTests;

/// <summary>
/// Tests for AdminController covering Epic 7: Statistics + CSV export (US-703)
/// </summary>
public class AdminControllerTests
{
	private readonly AdminController _controller;
	private readonly Mock<IUnitOfWork> _uowMock;

	public AdminControllerTests()
	{
		_uowMock = new Mock<IUnitOfWork>();
		_controller = new AdminController(_uowMock.Object)
		{
			ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext()
			}
		};
		SetAdmin();
	}

	private void SetAdmin()
	{
		var claims = new List<Claim>
		{
			new("id", TestConstants.DefaultUserId),
			new(ClaimTypes.Role, "Admin"),
			new(ClaimTypes.Name, "admin")
		};
		var identity = new ClaimsIdentity(claims, "TestAuthType");
		_controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
	}

	private void SetupEmptyRepos()
	{
		_uowMock.Setup(u => u.UserRepository.GetAsync(null, null))
			.ReturnsAsync(Result.Ok(new List<User>()));
		_uowMock.Setup(u => u.MediaRepository.GetAsync(null, null))
			.ReturnsAsync(Result.Ok(new List<Media>()));
		_uowMock.Setup(u => u.ReviewRepository.GetAsync(null, null))
			.ReturnsAsync(Result.Ok(new List<Review>()));
		_uowMock.Setup(u => u.CommentRepository.GetAsync(null, null))
			.ReturnsAsync(Result.Ok(new List<Comment>()));
		_uowMock.Setup(u => u.PlaylistRepository.GetAsync(null, null))
			.ReturnsAsync(Result.Ok(new List<Playlist>()));
		_uowMock.Setup(u => u.ReportRepository.GetAsync(null, null))
			.ReturnsAsync(Result.Ok(new List<Report>()));
		_uowMock.Setup(u => u.ReportRepository.GetAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Report, bool>>>(), null))
			.ReturnsAsync(Result.Ok(new List<Report>()));
		_uowMock.Setup(u => u.MediaTranslationRepository.GetAsync(It.IsAny<System.Linq.Expressions.Expression<Func<MediaTranslation, bool>>>(), null))
			.ReturnsAsync(Result.Ok(new List<MediaTranslation>()));
		_uowMock.Setup(u => u.TrackingStatusRepository.GetAsync(null, null))
			.ReturnsAsync(Result.Ok(new List<TrackingStatus>()));
		_uowMock.Setup(u => u.UserRepository.GetAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), null))
			.ReturnsAsync(Result.Ok(new List<User>()));
		_uowMock.Setup(u => u.ReviewRepository.GetAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Review, bool>>>(), null))
			.ReturnsAsync(Result.Ok(new List<Review>()));
	}

	#region US-703: Statistics

	[Fact]
	public async Task GetStats_Returns200WithCounts()
	{
		var users = new List<User>
		{
			new() { Id = Guid.NewGuid(), Username = "user1", Email = "a@b.com", PasswordHash = "h", PasswordSalt = "s" },
			new() { Id = Guid.NewGuid(), Username = "user2", Email = "c@d.com", PasswordHash = "h", PasswordSalt = "s" }
		};
		var reviews = new List<Review> { new() { Id = Guid.NewGuid() } };

		_uowMock.Setup(u => u.UserRepository.GetAsync(null, null)).ReturnsAsync(Result.Ok(users));
		_uowMock.Setup(u => u.MediaRepository.GetAsync(null, null)).ReturnsAsync(Result.Ok(new List<Media>()));
		_uowMock.Setup(u => u.ReviewRepository.GetAsync(null, null)).ReturnsAsync(Result.Ok(reviews));
		_uowMock.Setup(u => u.CommentRepository.GetAsync(null, null)).ReturnsAsync(Result.Ok(new List<Comment>()));
		_uowMock.Setup(u => u.PlaylistRepository.GetAsync(null, null)).ReturnsAsync(Result.Ok(new List<Playlist>()));
		_uowMock.Setup(u => u.ReportRepository.GetAsync(null, null)).ReturnsAsync(Result.Ok(new List<Report>()));
		_uowMock.Setup(u => u.ReportRepository.GetAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Report, bool>>>(), null))
			.ReturnsAsync(Result.Ok(new List<Report>()));
		_uowMock.Setup(u => u.MediaTranslationRepository.GetAsync(It.IsAny<System.Linq.Expressions.Expression<Func<MediaTranslation, bool>>>(), null))
			.ReturnsAsync(Result.Ok(new List<MediaTranslation>()));
		_uowMock.Setup(u => u.TrackingStatusRepository.GetAsync(null, null))
			.ReturnsAsync(Result.Ok(new List<TrackingStatus>()));
		_uowMock.Setup(u => u.UserRepository.GetAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), null))
			.ReturnsAsync(Result.Ok(new List<User>()));
		_uowMock.Setup(u => u.ReviewRepository.GetAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Review, bool>>>(), null))
			.ReturnsAsync(Result.Ok(new List<Review>()));

		var result = await _controller.GetStats(null, null);

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task GetStats_EmptyDb_ReturnsZeroCounts()
	{
		SetupEmptyRepos();

		var result = await _controller.GetStats(null, null);

		Assert.IsType<OkObjectResult>(result);
	}

	#endregion

	#region US-703: CSV export

	[Fact]
	public async Task ExportUsersCsv_ReturnsFileResult()
	{
		var users = new List<User>
		{
			new()
			{
				Id = Guid.NewGuid(), Username = "alice", Email = "alice@test.com",
				PasswordHash = "h", PasswordSalt = "s", Role = UserRole.User,
				CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
			}
		};

		_uowMock.Setup(u => u.UserRepository.GetAsync(null, null)).ReturnsAsync(Result.Ok(users));
		_uowMock.Setup(u => u.ReviewRepository.GetAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Review, bool>>>(), null))
			.ReturnsAsync(Result.Ok(new List<Review>()));
		_uowMock.Setup(u => u.PlaylistRepository.GetAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Playlist, bool>>>(), null))
			.ReturnsAsync(Result.Ok(new List<Playlist>()));
		_uowMock.Setup(u => u.FollowRepository.GetAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Follow, bool>>>(), null))
			.ReturnsAsync(Result.Ok(new List<Follow>()));

		var result = await _controller.ExportUsersCsv();

		var fileResult = Assert.IsType<FileContentResult>(result);
		Assert.Equal("text/csv; charset=utf-8", fileResult.ContentType);
		Assert.Equal("users_export.csv", fileResult.FileDownloadName);
	}

	[Fact]
	public async Task ExportUsersCsv_CsvHasHeaderRow()
	{
		_uowMock.Setup(u => u.UserRepository.GetAsync(null, null)).ReturnsAsync(Result.Ok(new List<User>()));
		_uowMock.Setup(u => u.ReviewRepository.GetAsync(null, null)).ReturnsAsync(Result.Ok(new List<Review>()));
		_uowMock.Setup(u => u.PlaylistRepository.GetAsync(null, null)).ReturnsAsync(Result.Ok(new List<Playlist>()));
		_uowMock.Setup(u => u.FollowRepository.GetAsync(null, null)).ReturnsAsync(Result.Ok(new List<Follow>()));

		var result = await _controller.ExportUsersCsv();

		var fileResult = Assert.IsType<FileContentResult>(result);
		var content = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
		Assert.Contains("Id,Username,Email,Role", content);
	}

	#endregion

	#region Authorization attributes

	[Fact]
	public void AdminController_HasAdminPolicyAttribute()
	{
		var controllerType = typeof(AdminController);
		var authAttrs = controllerType
			.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
			.Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().ToList();

		Assert.NotEmpty(authAttrs);
		Assert.Contains(authAttrs, a => a.Policy == "adminPolicy");
	}

	#endregion
}
