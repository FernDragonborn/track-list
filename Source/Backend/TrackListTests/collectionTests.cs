using System.Security.Claims;
using api;
using api.Controllers;
using api.DTOs;
using api.Enums;
using api.Services.IServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using static api.DTOs.ResponseTypes;

// ReSharper disable Reqnroll.MethodNameMismatchPattern

namespace TrackListTests;

/// <summary>
/// Tests for CollectionController covering Epic 8: Collections (US-801 → US-805)
/// </summary>
public class CollectionControllerTests
{
	private readonly CollectionController _controller;
	private readonly Mock<ICollectionService> _serviceMock;
	private static readonly Guid UserId = Guid.Parse(TestConstants.DefaultUserId);

	public CollectionControllerTests()
	{
		_serviceMock = new Mock<ICollectionService>();
		_controller = new CollectionController(_serviceMock.Object)
		{
			ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext()
			}
		};
	}

	private void SetUser(string role = "User", string userId = TestConstants.DefaultUserId)
	{
		var claims = new List<Claim>
		{
			new("id", userId),
			new(ClaimTypes.Role, role),
			new(ClaimTypes.Name, "testuser")
		};
		var identity = new ClaimsIdentity(claims, "TestAuthType");
		_controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
	}

	private void SetAnonymous()
	{
		_controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
	}

	#region US-801: Create collection

	[Fact]
	public async Task Create_Valid_Returns200WithData()
	{
		SetUser();
		var request = new CreateCollectionRequest { Name = "My List", PrivacyLevel = PlaylistPrivacyLevel.Public };
		var response = new CollectionResponseDto
		{
			Id = Guid.NewGuid(),
			Name = "My List",
			OwnerId = UserId,
			OwnerUsername = "testuser",
			PrivacyLevel = PlaylistPrivacyLevel.Public
		};

		_serviceMock
			.Setup(s => s.CreateAsync(UserId, It.IsAny<CreateCollectionRequest>()))
			.ReturnsAsync(Result.Ok(response));

		var result = await _controller.Create(request);

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task Create_ServiceFails_ReturnsBadRequest()
	{
		SetUser();
		var request = new CreateCollectionRequest { Name = "My List" };

		_serviceMock
			.Setup(s => s.CreateAsync(UserId, It.IsAny<CreateCollectionRequest>()))
			.ReturnsAsync(Result.Fail<CollectionResponseDto>("Creation failed"));

		var result = await _controller.Create(request);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task Create_NoAuth_ReturnsBadRequest()
	{
		SetAnonymous();
		var request = new CreateCollectionRequest { Name = "My List" };

		var result = await _controller.Create(request);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	#endregion

	#region US-801: Get collection

	[Fact]
	public async Task GetById_Found_Returns200()
	{
		SetUser();
		var collectionId = Guid.NewGuid();
		var detail = new CollectionDetailResponseDto
		{
			Id = collectionId,
			Name = "My List",
			OwnerId = UserId,
			OwnerUsername = "testuser",
			Items = [],
			SharedWith = []
		};

		_serviceMock
			.Setup(s => s.GetByIdAsync(collectionId, UserId))
			.ReturnsAsync(Result.Ok(detail));

		var result = await _controller.GetById(collectionId);

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task GetById_NotFound_Returns404()
	{
		SetUser();
		var collectionId = Guid.NewGuid();

		_serviceMock
			.Setup(s => s.GetByIdAsync(collectionId, UserId))
			.ReturnsAsync(Result.Fail<CollectionDetailResponseDto>("Collection not found"));

		var result = await _controller.GetById(collectionId);

		Assert.IsType<NotFoundObjectResult>(result);
	}

	[Fact]
	public async Task GetByOwner_ReturnsPaged()
	{
		SetUser();
		var paged = new PagedResponse<CollectionResponseDto>([], 0, 1, 10);

		_serviceMock
			.Setup(s => s.GetByOwnerAsync(UserId, UserId, 1, 10))
			.ReturnsAsync(Result.Ok(paged));

		var result = await _controller.GetByOwner(UserId);

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task GetPublic_ReturnsPaged()
	{
		var paged = new PagedResponse<CollectionResponseDto>([], 0, 1, 10);

		_serviceMock
			.Setup(s => s.GetPublicAsync(1, 10))
			.ReturnsAsync(Result.Ok(paged));

		var result = await _controller.GetPublic();

		Assert.IsType<OkObjectResult>(result);
	}

	#endregion

	#region US-801: Update collection

	[Fact]
	public async Task Update_Valid_Returns204()
	{
		SetUser();
		var collectionId = Guid.NewGuid();
		var request = new UpdateCollectionRequest { Name = "Renamed" };

		_serviceMock
			.Setup(s => s.UpdateAsync(collectionId, UserId, It.IsAny<UpdateCollectionRequest>()))
			.ReturnsAsync(Result.Ok());

		var result = await _controller.Update(collectionId, request);

		Assert.IsType<NoContentResult>(result);
	}

	[Fact]
	public async Task Update_NotOwner_ReturnsBadRequest()
	{
		SetUser();
		var collectionId = Guid.NewGuid();
		var request = new UpdateCollectionRequest { Name = "Renamed" };

		_serviceMock
			.Setup(s => s.UpdateAsync(collectionId, UserId, It.IsAny<UpdateCollectionRequest>()))
			.ReturnsAsync(Result.Fail("Only the owner can update this collection"));

		var result = await _controller.Update(collectionId, request);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	#endregion

	#region US-805: Delete collection

	[Fact]
	public async Task Delete_Owner_Returns204()
	{
		SetUser();
		var collectionId = Guid.NewGuid();

		_serviceMock
			.Setup(s => s.DeleteAsync(collectionId, UserId, false))
			.ReturnsAsync(Result.Ok());

		var result = await _controller.Delete(collectionId);

		Assert.IsType<NoContentResult>(result);
	}

	[Fact]
	public async Task Delete_Admin_Returns204()
	{
		SetUser("Admin");
		var collectionId = Guid.NewGuid();

		_serviceMock
			.Setup(s => s.DeleteAsync(collectionId, UserId, true))
			.ReturnsAsync(Result.Ok());

		var result = await _controller.Delete(collectionId);

		Assert.IsType<NoContentResult>(result);
	}

	[Fact]
	public async Task Delete_NotOwnerNotAdmin_ReturnsBadRequest()
	{
		SetUser();
		var collectionId = Guid.NewGuid();

		_serviceMock
			.Setup(s => s.DeleteAsync(collectionId, UserId, false))
			.ReturnsAsync(Result.Fail("Only the owner or an admin can delete this collection"));

		var result = await _controller.Delete(collectionId);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	#endregion

	#region US-802: Add/remove media items

	[Fact]
	public async Task AddItem_Valid_Returns200()
	{
		SetUser();
		var collectionId = Guid.NewGuid();
		var request = new AddCollectionItemRequest { MediaId = Guid.NewGuid() };
		var item = new CollectionItemDto
		{
			Id = Guid.NewGuid(),
			MediaId = request.MediaId,
			MediaTitle = "Test Movie"
		};

		_serviceMock
			.Setup(s => s.AddItemAsync(collectionId, UserId, It.IsAny<AddCollectionItemRequest>()))
			.ReturnsAsync(Result.Ok(item));

		var result = await _controller.AddItem(collectionId, request);

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task AddItem_Duplicate_ReturnsBadRequest()
	{
		SetUser();
		var collectionId = Guid.NewGuid();
		var request = new AddCollectionItemRequest { MediaId = Guid.NewGuid() };

		_serviceMock
			.Setup(s => s.AddItemAsync(collectionId, UserId, It.IsAny<AddCollectionItemRequest>()))
			.ReturnsAsync(Result.Fail<CollectionItemDto>("Media already in this collection"));

		var result = await _controller.AddItem(collectionId, request);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task RemoveItem_Valid_Returns204()
	{
		SetUser();
		var collectionId = Guid.NewGuid();
		var itemId = Guid.NewGuid();

		_serviceMock
			.Setup(s => s.RemoveItemAsync(collectionId, itemId, UserId))
			.ReturnsAsync(Result.Ok());

		var result = await _controller.RemoveItem(collectionId, itemId);

		Assert.IsType<NoContentResult>(result);
	}

	[Fact]
	public async Task ReorderItem_Valid_Returns204()
	{
		SetUser();
		var collectionId = Guid.NewGuid();
		var itemId = Guid.NewGuid();

		_serviceMock
			.Setup(s => s.ReorderItemAsync(collectionId, itemId, UserId, 3))
			.ReturnsAsync(Result.Ok());

		var result = await _controller.ReorderItem(collectionId, itemId, 3);

		Assert.IsType<NoContentResult>(result);
	}

	#endregion

	#region US-804: Granular access (sharing)

	[Fact]
	public async Task GrantAccess_Valid_Returns200()
	{
		SetUser();
		var collectionId = Guid.NewGuid();
		var targetUserId = Guid.NewGuid();
		var request = new GrantAccessRequest { UserId = targetUserId };
		var access = new CollectionAccessDto
		{
			Id = Guid.NewGuid(),
			UserId = targetUserId,
			Username = "friend"
		};

		_serviceMock
			.Setup(s => s.GrantAccessAsync(collectionId, UserId, It.IsAny<GrantAccessRequest>()))
			.ReturnsAsync(Result.Ok(access));

		var result = await _controller.GrantAccess(collectionId, request);

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task GrantAccess_Self_ReturnsBadRequest()
	{
		SetUser();
		var collectionId = Guid.NewGuid();
		var request = new GrantAccessRequest { UserId = UserId };

		_serviceMock
			.Setup(s => s.GrantAccessAsync(collectionId, UserId, It.IsAny<GrantAccessRequest>()))
			.ReturnsAsync(Result.Fail<CollectionAccessDto>("Cannot share with yourself"));

		var result = await _controller.GrantAccess(collectionId, request);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task RevokeAccess_Valid_Returns204()
	{
		SetUser();
		var collectionId = Guid.NewGuid();
		var targetUserId = Guid.NewGuid();

		_serviceMock
			.Setup(s => s.RevokeAccessAsync(collectionId, UserId, targetUserId))
			.ReturnsAsync(Result.Ok());

		var result = await _controller.RevokeAccess(collectionId, targetUserId);

		Assert.IsType<NoContentResult>(result);
	}

	#endregion

	#region Authorization attribute verification

	[Fact]
	public void Create_HasAuthorizeAttribute()
	{
		var method = typeof(CollectionController).GetMethod(nameof(CollectionController.Create));
		Assert.NotNull(method);
		var authAttr = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
		Assert.NotEmpty(authAttr);
	}

	[Fact]
	public void GetById_NoAuthorizeRequired()
	{
		var method = typeof(CollectionController).GetMethod(nameof(CollectionController.GetById));
		Assert.NotNull(method);
		var authAttr = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
		Assert.Empty(authAttr);
	}

	[Fact]
	public void Delete_HasAuthorizeAttribute()
	{
		var method = typeof(CollectionController).GetMethod(nameof(CollectionController.Delete));
		Assert.NotNull(method);
		var authAttr = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
		Assert.NotEmpty(authAttr);
	}

	#endregion
}
