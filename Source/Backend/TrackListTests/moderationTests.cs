using System.Security.Claims;
using api;
using api.Controllers;
using api.DTOs;
using api.Enums;
using api.Models;
using api.Repository.IReposotory;
using api.Services.IServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

// ReSharper disable Reqnroll.MethodNameMismatchPattern

namespace TrackListTests;

/// <summary>
/// Tests for ModerationController covering Epic 6: Translation moderation (US-603)
/// </summary>
public class ModerationControllerTests
{
	private readonly ModerationController _controller;
	private readonly Mock<IMediaOperationService> _mediaOpsMock;
	private readonly Mock<IUnitOfWork> _uowMock;
	private static readonly Guid ModeratorId = Guid.Parse(TestConstants.DefaultUserId);

	public ModerationControllerTests()
	{
		_mediaOpsMock = new Mock<IMediaOperationService>();
		_uowMock = new Mock<IUnitOfWork>();
		_controller = new ModerationController(_mediaOpsMock.Object, _uowMock.Object)
		{
			ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext()
			}
		};
		SetModerator();
	}

	private void SetModerator(string userId = TestConstants.DefaultUserId)
	{
		var claims = new List<Claim>
		{
			new("id", userId),
			new(ClaimTypes.Role, "Moderator"),
			new(ClaimTypes.Name, "moderator")
		};
		var identity = new ClaimsIdentity(claims, "TestAuthType");
		_controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
	}

	private void SetAnonymous()
	{
		_controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
	}

	#region US-603: Translation queue

	[Fact]
	public async Task GetPendingTranslations_ReturnsPaged()
	{
		var translations = new List<MediaTranslation>
		{
			new()
			{
				Id = Guid.NewGuid(), MediaId = Guid.NewGuid(), LanguageCode = "uk",
				Title = "Тест", Status = TranslationStatus.Pending,
				CreatedAt = DateTime.UtcNow
			}
		};

		_uowMock.Setup(u => u.MediaTranslationRepository.GetPagedAsync(
				It.IsAny<System.Linq.Expressions.Expression<Func<MediaTranslation, bool>>>(),
				It.IsAny<Func<IQueryable<MediaTranslation>, IOrderedQueryable<MediaTranslation>>>(),
				1, 20))
			.ReturnsAsync(Result.Ok<(List<MediaTranslation>, int)>((translations, 1)));
		_uowMock.Setup(u => u.MediaTranslationRepository.GetAsync(
				It.IsAny<System.Linq.Expressions.Expression<Func<MediaTranslation, bool>>>(), null))
			.ReturnsAsync(Result.Ok(new List<MediaTranslation>()));

		var result = await _controller.GetPendingTranslations();

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task GetPendingTranslations_Empty_ReturnsEmptyList()
	{
		_uowMock.Setup(u => u.MediaTranslationRepository.GetPagedAsync(
				It.IsAny<System.Linq.Expressions.Expression<Func<MediaTranslation, bool>>>(),
				It.IsAny<Func<IQueryable<MediaTranslation>, IOrderedQueryable<MediaTranslation>>>(),
				1, 20))
			.ReturnsAsync(Result.Ok<(List<MediaTranslation>, int)>(([], 0)));
		_uowMock.Setup(u => u.MediaTranslationRepository.GetAsync(
				It.IsAny<System.Linq.Expressions.Expression<Func<MediaTranslation, bool>>>(), null))
			.ReturnsAsync(Result.Ok(new List<MediaTranslation>()));

		var result = await _controller.GetPendingTranslations();

		Assert.IsType<OkObjectResult>(result);
	}

	#endregion

	#region US-603: Approve/reject translation

	[Fact]
	public async Task UpdateTranslationStatus_Approve_Returns204()
	{
		var translationId = Guid.NewGuid();
		var request = new TranslationStatusUpdateRequest { Status = TranslationStatus.Approved };

		_mediaOpsMock
			.Setup(s => s.UpdateTranslationStatusAsync(It.IsAny<MediaTranslationStatusChangeDto>()))
			.ReturnsAsync(Result.Ok());

		var result = await _controller.UpdateTranslationStatus(translationId, request);

		Assert.IsType<NoContentResult>(result);
		_mediaOpsMock.Verify(s => s.UpdateTranslationStatusAsync(
			It.Is<MediaTranslationStatusChangeDto>(d =>
				d.TranslationId == translationId &&
				d.Status == TranslationStatus.Approved &&
				d.ProcessedByUserId == ModeratorId)), Times.Once);
	}

	[Fact]
	public async Task UpdateTranslationStatus_Reject_Returns204()
	{
		var translationId = Guid.NewGuid();
		var request = new TranslationStatusUpdateRequest { Status = TranslationStatus.Rejected };

		_mediaOpsMock
			.Setup(s => s.UpdateTranslationStatusAsync(It.IsAny<MediaTranslationStatusChangeDto>()))
			.ReturnsAsync(Result.Ok());

		var result = await _controller.UpdateTranslationStatus(translationId, request);

		Assert.IsType<NoContentResult>(result);
	}

	[Fact]
	public async Task UpdateTranslationStatus_InvalidStatus_ReturnsBadRequest()
	{
		var translationId = Guid.NewGuid();
		var request = new TranslationStatusUpdateRequest { Status = TranslationStatus.Pending };

		var result = await _controller.UpdateTranslationStatus(translationId, request);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task UpdateTranslationStatus_NoAuth_ReturnsBadRequest()
	{
		SetAnonymous();
		var translationId = Guid.NewGuid();
		var request = new TranslationStatusUpdateRequest { Status = TranslationStatus.Approved };

		var result = await _controller.UpdateTranslationStatus(translationId, request);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	#endregion

	#region Authorization attributes

	[Fact]
	public void ModerationController_RequiresAdminOrModerator()
	{
		var controllerType = typeof(ModerationController);
		var authAttrs = controllerType
			.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
			.Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().ToList();

		Assert.NotEmpty(authAttrs);
		var rolesAttr = authAttrs.FirstOrDefault(a => !string.IsNullOrEmpty(a.Roles));
		Assert.NotNull(rolesAttr);
		Assert.Contains("Admin", rolesAttr!.Roles!);
		Assert.Contains("Moderator", rolesAttr.Roles!);
	}

	#endregion
}
