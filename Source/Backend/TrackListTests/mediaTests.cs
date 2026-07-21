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
/// Tests for MediaController covering Epic 4 (US-401, US-408) and Epic 9 (US-901, US-902)
/// </summary>
public class MediaControllerTests
{
	private readonly MediaController _controller;
	private readonly Mock<IMediaGetService> _mediaGetMock;
	private readonly Mock<IMediaOperationService> _mediaOpsMock;

	public MediaControllerTests()
	{
		_mediaGetMock = new Mock<IMediaGetService>();
		_mediaOpsMock = new Mock<IMediaOperationService>();
		_controller = new MediaController(_mediaGetMock.Object, _mediaOpsMock.Object, new Mock<IUnitOfWork>().Object, new Mock<IExternalContentService>().Object, new Mock<ITranslationService>().Object)
		{
			ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext()
			}
		};
	}

	#region US-401: View media page (multilingual)

	[Fact]
	public async Task GetMediaById_InternalGuid_ReturnsMedia()
	{
		var mediaId = Guid.NewGuid();
		var media = new Media
		{
			Id = mediaId,
			Type = MediaType.Movie,
			ReleaseYear = 2021,
			Translations = new List<MediaTranslation>
			{
				new() { LanguageCode = "uk", Title = "Дюна", Description = "Український опис" },
				new() { LanguageCode = "en", Title = "Dune", Description = "English desc" }
			}
		};

		_mediaGetMock
			.Setup(s => s.GetByIdAsync(mediaId.ToString(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Result.Ok(media));

		var result = await _controller.GetMediaById(mediaId.ToString(), CancellationToken.None);

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task GetMediaById_ExternalId_ReturnsMedia()
	{
		// US-902: external ID format like "Tmdb:movie:123"
		var externalId = "Tmdb:movie:438631";
		var media = new Media
		{
			Id = Guid.NewGuid(),
			ExternalApiId = externalId,
			Type = MediaType.Movie
		};

		_mediaGetMock
			.Setup(s => s.GetByIdAsync(externalId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(Result.Ok(media));

		var result = await _controller.GetMediaById(externalId, CancellationToken.None);

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task GetMediaById_NotFound_Returns404()
	{
		_mediaGetMock
			.Setup(s => s.GetByIdAsync("nonexistent", It.IsAny<CancellationToken>()))
			.ReturnsAsync(Result.Fail<Media>("Media not found"));

		var result = await _controller.GetMediaById("nonexistent", CancellationToken.None);

		Assert.IsType<NotFoundObjectResult>(result);
	}

	#endregion

	#region US-901: Search media

	[Fact]
	public async Task Search_ValidQuery_ReturnsResults()
	{
		var results = new List<Media>
		{
			new()
			{
				Id = Guid.NewGuid(),
				ExternalApiId = "tmdb:438631",
				Type = MediaType.Movie,
				Translations = new List<MediaTranslation>
				{
					new() { Title = "Dune", LanguageCode = "en" }
				}
			}
		};

		_mediaGetMock
			.Setup(s => s.SearchAsync("Dune", It.IsAny<CancellationToken>()))
			.ReturnsAsync(Result.Ok(results));

		var result = await _controller.Search("Dune", CancellationToken.None);

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task Search_EmptyQuery_ReturnsBadRequest()
	{
		var result = await _controller.Search("", CancellationToken.None);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task Search_WhitespaceQuery_ReturnsBadRequest()
	{
		var result = await _controller.Search("   ", CancellationToken.None);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task Search_NoResults_Returns404()
	{
		_mediaGetMock
			.Setup(s => s.SearchAsync("xyznonexistent", It.IsAny<CancellationToken>()))
			.ReturnsAsync(Result.Fail<List<Media>>("No results found"));

		var result = await _controller.Search("xyznonexistent", CancellationToken.None);

		Assert.IsType<NotFoundObjectResult>(result);
	}

	#endregion

	#region US-408: User translation suggestions

	[Fact]
	public async Task AddTranslation_ValidData_ReturnsOk()
	{
		var mediaId = Guid.NewGuid();
		var media = new Media { Id = mediaId, Type = MediaType.Movie };

		_mediaGetMock
			.Setup(s => s.GetByIdAsync(mediaId.ToString(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Result.Ok(media));

		var translationDto = new MediaTranslationDto
		{
			LanguageCode = "uk",
			Title = "Початок",
			Description = "Фільм про сни..."
		};

		var resultDto = new MediaTranslationDto
		{
			Id = Guid.NewGuid(),
			MediaId = mediaId,
			LanguageCode = "uk",
			Title = "Початок"
		};

		_mediaOpsMock
			.Setup(s => s.AddTranslationAsync(It.IsAny<MediaTranslationDto>()))
			.ReturnsAsync(Result.Ok(resultDto));

		var result = await _controller.AddTranslation(mediaId.ToString(), translationDto, CancellationToken.None);

		Assert.IsType<OkObjectResult>(result);
		_mediaOpsMock.Verify(s => s.AddTranslationAsync(
			It.Is<MediaTranslationDto>(t => t.MediaId == mediaId && t.LanguageCode == "uk")), Times.Once);
	}

	[Fact]
	public async Task AddTranslation_MediaNotFound_Returns404()
	{
		_mediaGetMock
			.Setup(s => s.GetByIdAsync("nonexistent", It.IsAny<CancellationToken>()))
			.ReturnsAsync(Result.Fail<Media>("Media not found"));

		var translationDto = new MediaTranslationDto
		{
			LanguageCode = "uk",
			Title = "Тест"
		};

		var result = await _controller.AddTranslation("nonexistent", translationDto, CancellationToken.None);

		Assert.IsType<NotFoundObjectResult>(result);
	}

	[Fact]
	public async Task AddTranslation_ServiceFails_ReturnsBadRequest()
	{
		var mediaId = Guid.NewGuid();
		var media = new Media { Id = mediaId };

		_mediaGetMock
			.Setup(s => s.GetByIdAsync(mediaId.ToString(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Result.Ok(media));

		_mediaOpsMock
			.Setup(s => s.AddTranslationAsync(It.IsAny<MediaTranslationDto>()))
			.ReturnsAsync(Result.Fail<MediaTranslationDto>("Translation already exists for this language"));

		var dto = new MediaTranslationDto { LanguageCode = "uk", Title = "Дюна" };
		var result = await _controller.AddTranslation(mediaId.ToString(), dto, CancellationToken.None);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	#endregion

	#region US-702: Media CRUD (Admin operations)

	[Fact]
	public async Task CreateMedia_Valid_Returns201()
	{
		var mediaDto = new MediaDto
		{
			Type = MediaType.Movie,
			ReleaseYear = 2021,
			Translations = new List<MediaTranslationDto>
			{
				new() { LanguageCode = "en", Title = "New Movie" }
			}
		};

		var createdDto = new MediaDto
		{
			Id = Guid.NewGuid(),
			Type = MediaType.Movie,
			ReleaseYear = 2021
		};

		_mediaOpsMock
			.Setup(s => s.AddAsync(It.IsAny<MediaDto>()))
			.ReturnsAsync(Result.Ok(createdDto));

		var result = await _controller.CreateMedia(mediaDto);

		var createdResult = Assert.IsType<CreatedAtActionResult>(result);
		Assert.Equal(201, createdResult.StatusCode);
	}

	[Fact]
	public async Task UpdateMedia_Valid_ReturnsNoContent()
	{
		var mediaDto = new MediaDto { Id = Guid.NewGuid(), Type = MediaType.Movie };

		_mediaOpsMock
			.Setup(s => s.UpdateAsync(It.IsAny<MediaDto>()))
			.ReturnsAsync(Result.Ok());

		var result = await _controller.UpdateMedia(mediaDto);

		Assert.IsType<NoContentResult>(result);
	}

	[Fact]
	public async Task DeleteMedia_Valid_ReturnsNoContent()
	{
		var id = Guid.NewGuid().ToString();

		_mediaOpsMock
			.Setup(s => s.DeleteAsync(id))
			.ReturnsAsync(Result.Ok());

		var result = await _controller.DeleteMedia(id);

		Assert.IsType<NoContentResult>(result);
	}

	[Fact]
	public async Task DeleteMedia_NotFound_ReturnsBadRequest()
	{
		_mediaOpsMock
			.Setup(s => s.DeleteAsync("bad-id"))
			.ReturnsAsync(Result.Fail("Media not found"));

		var result = await _controller.DeleteMedia("bad-id");

		Assert.IsType<BadRequestObjectResult>(result);
	}

	#endregion

	#region Translation CRUD

	[Fact]
	public async Task UpdateTranslation_IdMismatch_ReturnsBadRequest()
	{
		var routeId = Guid.NewGuid();
		var bodyId = Guid.NewGuid();

		var dto = new MediaTranslationDto { Id = bodyId, Title = "Updated" };
		var result = await _controller.UpdateTranslation(routeId, dto);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task UpdateTranslation_Valid_ReturnsNoContent()
	{
		var translationId = Guid.NewGuid();
		var dto = new MediaTranslationDto { Id = translationId, Title = "Updated Title" };

		_mediaOpsMock
			.Setup(s => s.UpdateTranslationAsync(It.IsAny<MediaTranslationDto>()))
			.ReturnsAsync(Result.Ok());

		var result = await _controller.UpdateTranslation(translationId, dto);

		Assert.IsType<NoContentResult>(result);
	}

	[Fact]
	public async Task DeleteTranslation_Valid_ReturnsNoContent()
	{
		var id = Guid.NewGuid().ToString();

		_mediaOpsMock
			.Setup(s => s.DeleteTranslationAsync(id))
			.ReturnsAsync(Result.Ok());

		var result = await _controller.DeleteTranslation(id);

		Assert.IsType<NoContentResult>(result);
	}

	#endregion
}
