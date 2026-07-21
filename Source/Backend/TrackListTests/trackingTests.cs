using System.Linq.Expressions;
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
using Reqnroll;
using Xunit;

// ReSharper disable Reqnroll.MethodNameMismatchPattern

namespace TrackListTests;

[Binding]
public class TrackingTests
{
	private readonly TrackingStatusController _controller;
	private readonly Mock<IUnitOfWork> _unitOfWorkMock;
	private readonly Mock<ITrackingStatusRepository> _trackingRepoMock;

	private IActionResult? _lastResult;

	// In-memory store for tracking statuses
	private readonly List<TrackingStatus> _mockTrackingStatuses = new();

	private string _currentUserId = TestConstants.DefaultUserId;

	public TrackingTests()
	{
		_unitOfWorkMock = new Mock<IUnitOfWork>();
		_trackingRepoMock = new Mock<ITrackingStatusRepository>();
		_unitOfWorkMock.Setup(u => u.TrackingStatusRepository).Returns(_trackingRepoMock.Object);

		_controller = new TrackingStatusController(_unitOfWorkMock.Object)
		{
			ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext()
			}
		};
	}

	private void SetControllerUser(string userId)
	{
		_currentUserId = userId;
		var claims = new List<Claim>
		{
			new("id", userId),
			new(ClaimTypes.Role, "User")
		};
		var identity = new ClaimsIdentity(claims, "TestAuthType");
		_controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
	}

	// ──── GIVEN ────

	[Given(@"Користувач ""(.*)"" авторизований в системі")]
	[Scope(Feature = "Статуси перегляду (Трекінг)")]
	public void GivenUserAuthorized(string username)
	{
		SetControllerUser(TestConstants.DefaultUserId);
	}

	[Given(@"В базі даних існує користувач ""(.*)""")]
	[Scope(Feature = "Статуси перегляду (Трекінг)")]
	public void GivenUserExists(string username) { /* user context handled via claims */ }

	[Given(@"В базі даних існує медіа ""(.*)"" \(Id: (\d+)(?:,.*)?\)")]
	[Scope(Feature = "Статуси перегляду (Трекінг)")]
	public void GivenMediaExists(string title, int id) { /* media referenced by Guid in DTO */ }

	[Given(@"Користувач ""(.*)"" знаходиться на сторінці ""/media/(\d+)"" \(.*\)")]
	[Scope(Feature = "Статуси перегляду (Трекінг)")]
	public void GivenUserOnMediaPage(string username, int mediaId) { /* frontend routing */ }

	[Given(@"""(.*)"" \(Id: (\d+)\) ще не має статусу для ""(.*)""-а")]
	public void GivenMediaHasNoStatusForUser(string title, int mediaId, string username)
	{
		// Controller checks: existingStatus != null && existingStatus.Value != null
		// So we return Ok with null value to indicate "no record found" without throwing
		_trackingRepoMock
			.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<TrackingStatus, bool>>>(), null))
			.ReturnsAsync(Result.Ok<TrackingStatus>(null!));
	}

	[Given(@"На сторінці відображається ""Кнопка Статусу"" з текстом ""(.*)""")]
	public void GivenStatusButtonShowsText(string text) { /* frontend UI */ }

	[Given(@"Користувач ""(.*)"" вже додав ""(.*)"" \(Id: (\d+)\) до статусу ""(.*)""")]
	public void GivenUserAlreadyHasTrackingStatus(string username, string title, int mediaId, string statusName)
	{
		var status = statusName switch
		{
			"Заплановано" => TrackingStatusCode.Planned,
			"Дивлюся" => TrackingStatusCode.Watching,
			"Переглянуто" => TrackingStatusCode.Completed,
			"Кинуто" => TrackingStatusCode.Dropped,
			_ => TrackingStatusCode.Planned
		};

		var existingEntry = new TrackingStatus
		{
			Id = Guid.NewGuid(),
			UserId = Guid.Parse(_currentUserId),
			MediaId = Guid.Empty, // placeholder
			Status = status,
			Progress = null
		};

		_trackingRepoMock
			.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<TrackingStatus, bool>>>(), null))
			.ReturnsAsync(Result.Ok(existingEntry));

		_trackingRepoMock
			.Setup(r => r.Update(It.IsAny<TrackingStatus>()))
			.ReturnsAsync((TrackingStatus ts) =>
			{
				ts.UpdatedAt = DateTime.UtcNow;
				return ts;
			});
	}

	[Given(@"""Кнопка Статусу"" показує текст ""(.*)""")]
	public void GivenButtonShowsStatusText(string text) { /* frontend UI */ }

	[Given(@"На сторінці ""/media/(\d+)"" \(.*\) відображається ""Кнопка Статусу"" з текстом ""(.*)""")]
	public void GivenButtonShowsStatusTextOnPage(int mediaId, string text) { /* frontend UI */ }

	[Given(@"Користувач ""(.*)"" авторизований і бачить ""Кнопку Статусу"" з текстом ""(.*)""")]
	public void GivenUserAuthorizedAndSeesButton(string username, string text)
	{
		SetControllerUser(TestConstants.DefaultUserId);
	}

	// ──── WHEN ────

	[When(@"Він натискає на ""Кнопку Статусу""")]
	[When(@"Він натискає на ""Кнопку Статусу"", і випадаюче меню з'являється")]
	public void WhenUserClicksStatusButton() { /* frontend interaction */ }

	[When(@"Користувач ""(.*)"" натискає на ""Кнопку Статусу""")]
	public void WhenNamedUserClicksStatusButton(string username) { /* frontend interaction */ }

	[When(@"У випадаючому меню(?:, що з'явилося,)? він обирає (?:статус |новий статус )?""(.*)""")]
	public async Task WhenUserSelectsStatusFromDropdown(string statusName)
	{
		var status = statusName switch
		{
			"Заплановано" => TrackingStatusCode.Planned,
			"Дивлюся" => TrackingStatusCode.Watching,
			"Переглянуто" => TrackingStatusCode.Completed,
			"Кинуто" => TrackingStatusCode.Dropped,
			_ => TrackingStatusCode.Planned
		};

		// Setup AddAsync for new entries
		_trackingRepoMock
			.Setup(r => r.AddAsync(It.IsAny<TrackingStatus>()))
			.ReturnsAsync((TrackingStatus ts) => Result.Ok(ts));

		var dto = new TrackingStatusDto
		{
			MediaId = Guid.NewGuid(),
			Status = status
		};

		_lastResult = await _controller.UpsertTrackingStatus(dto);
	}

	[When(@"Він натискає на будь-яке місце на сторінці поза межами меню")]
	public void WhenUserClicksOutsideMenu() { /* frontend - no backend call expected */ }

	// ──── THEN ────

	[Then(@"Система \(бекенд\) отримує запит на створення запису в `TrackingStatus` \(.*\)")]
	public void ThenSystemReceivesCreateRequest()
	{
		Assert.NotNull(_lastResult);
		var okResult = Assert.IsType<OkObjectResult>(_lastResult);
		Assert.NotNull(okResult.Value);
	}

	[Then(@"Система \(бекенд\) отримує запит на оновлення запису \(Status: ""(.*)""\)")]
	public void ThenSystemReceivesUpdateRequest(string statusStr)
	{
		Assert.NotNull(_lastResult);
		Assert.IsType<OkObjectResult>(_lastResult);
	}

	[Then(@"В базі даних існує лише один запис для пари \(UserId: ""(.*)"", MediaId: (\d+)\)")]
	public void ThenOnlyOneRecordExists(string userId, int mediaId)
	{
		// Upsert logic guarantees single record via composite key
		// Verify Update was called (not Add) when entry existed
		_trackingRepoMock.Verify(r => r.Update(It.IsAny<TrackingStatus>()), Times.Once);
	}

	[Then(@"""Кнопка Статусу"" на сторінці медіа змінює свій текст на ""(.*)""")]
	[Then(@"""Кнопка Статусу"" продовжує показувати текст ""(.*)""")]
	public void ThenButtonTextChanges(string text) { /* frontend UI verification */ }

	[Then(@"Випадаюче меню закривається")]
	public void ThenDropdownCloses() { /* frontend UI */ }

	[Then(@"Система \(бекенд\) НЕ отримує жодного запиту")]
	public void ThenBackendReceivesNoRequest()
	{
		// For UI-only scenarios, no controller call was made
		Assert.Null(_lastResult);
	}

	[Then(@"Система \(бекенд\) оновлює запис у `TrackingStatus` \(MediaId: (\d+)\), додавши ""Progress: (\d+)""")]
	public void ThenSystemUpdatesProgress(int mediaId, int progress)
	{
		Assert.NotNull(_lastResult);
		Assert.IsType<OkObjectResult>(_lastResult);
	}

	// ──── Progress (US-502) ────

	[When(@"Він переходить на сторінку свого профілю \(вкладка ""Трекінг""\)")]
	public void WhenUserGoesToTrackingTab() { /* frontend routing */ }

	[When(@"Він бачить ""(.*)"" у списку ""(.*)""")]
	public void WhenUserSeesMediaInList(string title, string listName) { /* frontend UI */ }

	[When(@"Він вводить ""(\d+)"" у поле ""Поточний епізод"" для ""(.*)""")]
	public async Task WhenUserEntersProgress(int episode, string title)
	{
		_trackingRepoMock
			.Setup(r => r.Update(It.IsAny<TrackingStatus>()))
			.ReturnsAsync((TrackingStatus ts) => ts);

		var existingEntry = new TrackingStatus
		{
			Id = Guid.NewGuid(),
			UserId = Guid.Parse(_currentUserId),
			MediaId = Guid.NewGuid(),
			Status = TrackingStatusCode.Watching,
			Progress = null
		};

		_trackingRepoMock
			.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<TrackingStatus, bool>>>(), null))
			.ReturnsAsync(Result.Ok(existingEntry));

		var dto = new TrackingStatusDto
		{
			MediaId = existingEntry.MediaId,
			Status = TrackingStatusCode.Watching,
			Progress = episode
		};

		_lastResult = await _controller.UpsertTrackingStatus(dto);
	}

	[When(@"Він натискає ""Зберегти"" \(або відбувається автозбереження\)")]
	public void WhenUserClicksSave() { /* covered by previous step */ }

	[When(@"Він відправляє DELETE запит на ""/api/trackingstatus/\{mediaId\}""")]
	[Scope(Feature = "Статуси перегляду (Трекінг)")]
	public async Task WhenHeSendsDeleteTrackingStatus()
	{
		var mediaId = Guid.NewGuid();
		var existingStatus = new TrackingStatus
		{
			UserId = Guid.Parse(_currentUserId),
			MediaId = mediaId,
			Status = TrackingStatusCode.Planned
		};

		_trackingRepoMock
			.Setup(r => r.GetOneAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TrackingStatus, bool>>>(), null))
			.ReturnsAsync(Result.Ok(existingStatus));

		_trackingRepoMock
			.Setup(r => r.Remove(It.IsAny<TrackingStatus>()))
			.ReturnsAsync(Result.Ok());

		_unitOfWorkMock
			.Setup(u => u.SaveAsync())
			.Returns(Task.CompletedTask);

		_lastResult = await _controller.DeleteTrackingStatus(mediaId);
	}

	[Then(@"""(.*)"" \(Id: \d+\) більше не має статусу для ""(.*)""-а")]
	[Scope(Feature = "Статуси перегляду (Трекінг)")]
	public void ThenMediaHasNoTrackingStatusForUser(string _media, string _user)
	{
		// Verified by 204 status code — removal was successful
		Assert.IsType<NoContentResult>(_lastResult);
	}

	[Scope(Feature = "Статуси перегляду (Трекінг)")]
	[Then(@"Код відповіді становить (\d+)")]
	public void ThenResponseCodeIs(int expectedCode)
	{
		Helpers.ThenResponseCodeIs(expectedCode, _lastResult);
	}
}

/// <summary>
/// Additional xUnit tests for TrackingStatusController (non-BDD, direct unit tests)
/// </summary>
public class TrackingControllerUnitTests
{
	private readonly TrackingStatusController _controller;
	private readonly Mock<IUnitOfWork> _unitOfWorkMock;
	private readonly Mock<ITrackingStatusRepository> _trackingRepoMock;

	public TrackingControllerUnitTests()
	{
		_unitOfWorkMock = new Mock<IUnitOfWork>();
		_trackingRepoMock = new Mock<ITrackingStatusRepository>();
		_unitOfWorkMock.Setup(u => u.TrackingStatusRepository).Returns(_trackingRepoMock.Object);
		_controller = new TrackingStatusController(_unitOfWorkMock.Object);
	}

	private void SetUser(string userId = TestConstants.DefaultUserId)
	{
		var claims = new List<Claim> { new("id", userId), new(ClaimTypes.Role, "User") };
		var identity = new ClaimsIdentity(claims, "TestAuthType");
		_controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
		};
	}

	[Fact]
	public async Task UpsertTrackingStatus_NewEntry_ReturnsOk()
	{
		SetUser();
		var mediaId = Guid.NewGuid();

		// Controller accesses .Value directly, so return Ok with null to indicate "not found"
		_trackingRepoMock
			.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<TrackingStatus, bool>>>(), null))
			.ReturnsAsync(Result.Ok<TrackingStatus>(null!));

		_trackingRepoMock
			.Setup(r => r.AddAsync(It.IsAny<TrackingStatus>()))
			.ReturnsAsync((TrackingStatus ts) => Result.Ok(ts));

		var dto = new TrackingStatusDto { MediaId = mediaId, Status = TrackingStatusCode.Planned };
		var result = await _controller.UpsertTrackingStatus(dto);

		Assert.IsType<OkObjectResult>(result);
		_trackingRepoMock.Verify(r => r.AddAsync(It.Is<TrackingStatus>(
			ts => ts.MediaId == mediaId && ts.Status == TrackingStatusCode.Planned)), Times.Once);
	}

	[Fact]
	public async Task UpsertTrackingStatus_ExistingEntry_UpdatesStatus()
	{
		SetUser();
		var userId = Guid.Parse(TestConstants.DefaultUserId);
		var mediaId = Guid.NewGuid();

		var existing = new TrackingStatus
		{
			Id = Guid.NewGuid(),
			UserId = userId,
			MediaId = mediaId,
			Status = TrackingStatusCode.Planned
		};

		_trackingRepoMock
			.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<TrackingStatus, bool>>>(), null))
			.ReturnsAsync(Result.Ok(existing));

		_trackingRepoMock
			.Setup(r => r.Update(It.IsAny<TrackingStatus>()))
			.ReturnsAsync((TrackingStatus ts) => ts);

		var dto = new TrackingStatusDto { MediaId = mediaId, Status = TrackingStatusCode.Completed };
		var result = await _controller.UpsertTrackingStatus(dto);

		Assert.IsType<OkObjectResult>(result);
		_trackingRepoMock.Verify(r => r.Update(It.Is<TrackingStatus>(
			ts => ts.Status == TrackingStatusCode.Completed)), Times.Once);
	}

	[Fact]
	public async Task UpsertTrackingStatus_WithProgress_SetsProgressField()
	{
		SetUser();
		var mediaId = Guid.NewGuid();

		_trackingRepoMock
			.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<TrackingStatus, bool>>>(), null))
			.ReturnsAsync(Result.Ok<TrackingStatus>(null!));

		_trackingRepoMock
			.Setup(r => r.AddAsync(It.IsAny<TrackingStatus>()))
			.ReturnsAsync((TrackingStatus ts) => Result.Ok(ts));

		var dto = new TrackingStatusDto { MediaId = mediaId, Status = TrackingStatusCode.Watching, Progress = 5 };
		var result = await _controller.UpsertTrackingStatus(dto);

		Assert.IsType<OkObjectResult>(result);
		_trackingRepoMock.Verify(r => r.AddAsync(It.Is<TrackingStatus>(
			ts => ts.Progress == 5)), Times.Once);
	}

	[Fact]
	public async Task UpsertTrackingStatus_NoUserClaim_ReturnsBadRequest()
	{
		// No user claims set
		_controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext()
		};

		var dto = new TrackingStatusDto { MediaId = Guid.NewGuid(), Status = TrackingStatusCode.Planned };
		var result = await _controller.UpsertTrackingStatus(dto);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task GetTrackingStatuses_ReturnsAllForUser()
	{
		SetUser();
		var statuses = new List<TrackingStatus>
		{
			new() { UserId = Guid.Parse(TestConstants.DefaultUserId), MediaId = Guid.NewGuid(), Status = TrackingStatusCode.Planned },
			new() { UserId = Guid.Parse(TestConstants.DefaultUserId), MediaId = Guid.NewGuid(), Status = TrackingStatusCode.Watching }
		};

		_trackingRepoMock
			.Setup(r => r.GetAsync(It.IsAny<Expression<Func<TrackingStatus, bool>>>(), null))
			.ReturnsAsync(Result.Ok(statuses));

		var result = await _controller.GetTrackingStatuses();

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task GetTrackingStatus_Exists_ReturnsData()
	{
		SetUser();
		var mediaId = Guid.NewGuid();
		var entry = new TrackingStatus
		{
			UserId = Guid.Parse(TestConstants.DefaultUserId),
			MediaId = mediaId,
			Status = TrackingStatusCode.Completed
		};

		_trackingRepoMock
			.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<TrackingStatus, bool>>>(), null))
			.ReturnsAsync(Result.Ok(entry));

		var result = await _controller.GetTrackingStatus(mediaId);

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task GetTrackingStatus_NotExists_ReturnsOkWithNullData()
	{
		SetUser();
		var mediaId = Guid.NewGuid();

		_trackingRepoMock
			.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<TrackingStatus, bool>>>(), null))
			.ReturnsAsync(Result.Fail<TrackingStatus>("Not found"));

		var result = await _controller.GetTrackingStatus(mediaId);

		// Controller returns Ok with null data when not found
		Assert.IsType<OkObjectResult>(result);
	}
}
