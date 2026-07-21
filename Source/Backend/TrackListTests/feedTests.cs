using System.Security.Claims;
using api;
using api.Controllers;
using api.DTOs;
using api.Services.IServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using static api.DTOs.ResponseTypes;

// ReSharper disable Reqnroll.MethodNameMismatchPattern

namespace TrackListTests;

/// <summary>
/// Tests for FeedController covering Epic 3: Feed (US-301 → US-304)
/// </summary>
public class FeedControllerTests
{
	private readonly FeedController _controller;
	private readonly Mock<IFeedService> _serviceMock;
	private static readonly Guid UserId = Guid.Parse(TestConstants.DefaultUserId);

	public FeedControllerTests()
	{
		_serviceMock = new Mock<IFeedService>();
		_controller = new FeedController(_serviceMock.Object)
		{
			ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext()
			}
		};
	}

	private void SetUser(string userId = TestConstants.DefaultUserId)
	{
		var claims = new List<Claim>
		{
			new("id", userId),
			new(ClaimTypes.Role, "User"),
			new(ClaimTypes.Name, "testuser")
		};
		var identity = new ClaimsIdentity(claims, "TestAuthType");
		_controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
	}

	private void SetAnonymous()
	{
		_controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
	}

	#region US-301: Personal feed

	[Fact]
	public async Task PersonalFeed_Valid_Returns200()
	{
		SetUser();
		var paged = new PagedResponse<FeedItemDto>(
			[new FeedItemDto { ReviewId = Guid.NewGuid(), Username = "friend", Rating = 4 }],
			1, 1, 10);

		_serviceMock
			.Setup(s => s.GetPersonalFeedAsync(UserId, 1, 10))
			.ReturnsAsync(Result.Ok(paged));

		var result = await _controller.GetPersonalFeed();

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task PersonalFeed_Empty_Returns200WithEmptyItems()
	{
		SetUser();
		var paged = new PagedResponse<FeedItemDto>([], 0, 1, 10);

		_serviceMock
			.Setup(s => s.GetPersonalFeedAsync(UserId, 1, 10))
			.ReturnsAsync(Result.Ok(paged));

		var result = await _controller.GetPersonalFeed();

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task PersonalFeed_NoAuth_ReturnsBadRequest()
	{
		SetAnonymous();

		var result = await _controller.GetPersonalFeed();

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task PersonalFeed_ServiceFails_ReturnsBadRequest()
	{
		SetUser();

		_serviceMock
			.Setup(s => s.GetPersonalFeedAsync(UserId, 1, 10))
			.ReturnsAsync(Result.Fail<PagedResponse<FeedItemDto>>("DB error"));

		var result = await _controller.GetPersonalFeed();

		Assert.IsType<BadRequestObjectResult>(result);
	}

	#endregion

	#region US-302: Global feed

	[Fact]
	public async Task GlobalFeed_Authenticated_Returns200()
	{
		SetUser();
		var paged = new PagedResponse<FeedItemDto>(
			[new FeedItemDto { ReviewId = Guid.NewGuid(), Rating = 5 }],
			1, 1, 10);

		_serviceMock
			.Setup(s => s.GetGlobalFeedAsync(UserId, 1, 10))
			.ReturnsAsync(Result.Ok(paged));

		var result = await _controller.GetGlobalFeed();

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task GlobalFeed_Guest_Returns200()
	{
		SetAnonymous();
		var paged = new PagedResponse<FeedItemDto>([], 0, 1, 10);

		_serviceMock
			.Setup(s => s.GetGlobalFeedAsync((Guid?)null, 1, 10))
			.ReturnsAsync(Result.Ok(paged));

		var result = await _controller.GetGlobalFeed();

		Assert.IsType<OkObjectResult>(result);
	}

	#endregion

	#region Authorization attributes

	[Fact]
	public void PersonalFeed_HasAuthorizeAttribute()
	{
		var method = typeof(FeedController).GetMethod(nameof(FeedController.GetPersonalFeed));
		Assert.NotNull(method);
		var authAttr = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
		Assert.NotEmpty(authAttr);
	}

	[Fact]
	public void GlobalFeed_NoAuthorizeRequired()
	{
		var method = typeof(FeedController).GetMethod(nameof(FeedController.GetGlobalFeed));
		Assert.NotNull(method);
		var authAttr = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
		Assert.Empty(authAttr);
	}

	#endregion
}
