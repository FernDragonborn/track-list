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

// ReSharper disable Reqnroll.MethodNameMismatchPattern

namespace TrackListTests;

/// <summary>
/// Tests for ReportController covering Epic 6: Moderation (US-601, US-602)
/// </summary>
public class ReportControllerTests
{
	private readonly ReportController _controller;
	private readonly Mock<IReportService> _reportServiceMock;

	public ReportControllerTests()
	{
		_reportServiceMock = new Mock<IReportService>();
		_controller = new ReportController(_reportServiceMock.Object)
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

	#region US-601: Create report (user flags content)

	[Fact]
	public async Task CreateReport_Valid_Returns201()
	{
		SetUser();
		var reportDto = new ReportDto
		{
			TargetId = Guid.NewGuid(),
			TargetType = ReportableEntityType.Review,
			Reason = ReportReason.Spam,
			Comment = "Спам",
			ReporterId = Guid.Parse(TestConstants.DefaultUserId),
			Status = ReportStatus.Pending
		};

		var createdReport = new ReportDto
		{
			Id = Guid.NewGuid(),
			TargetId = reportDto.TargetId,
			TargetType = reportDto.TargetType,
			Reason = reportDto.Reason,
			Comment = reportDto.Comment,
			ReporterId = reportDto.ReporterId,
			Status = ReportStatus.Pending
		};

		_reportServiceMock
			.Setup(s => s.CreateAsync(It.IsAny<ReportDto>()))
			.ReturnsAsync(Result.Ok(createdReport));

		var result = await _controller.CreateReport(reportDto);

		var createdResult = Assert.IsType<CreatedAtActionResult>(result);
		Assert.Equal(201, createdResult.StatusCode);
	}

	[Fact]
	public async Task CreateReport_ServiceFails_ReturnsBadRequest()
	{
		SetUser();
		var reportDto = new ReportDto
		{
			TargetId = Guid.NewGuid(),
			TargetType = ReportableEntityType.Review,
			Reason = ReportReason.Spam
		};

		_reportServiceMock
			.Setup(s => s.CreateAsync(It.IsAny<ReportDto>()))
			.ReturnsAsync(Result.Fail<ReportDto>("Failed to create report"));

		var result = await _controller.CreateReport(reportDto);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	#endregion

	#region US-602: Moderator views and handles reports

	[Fact]
	public async Task GetReports_NullFilter_ReturnsAll()
	{
		SetUser("Moderator");
		var reports = new List<ReportDto>
		{
			new() { Id = Guid.NewGuid(), Status = ReportStatus.Pending },
			new() { Id = Guid.NewGuid(), Status = ReportStatus.ResolvedDeleted }
		};

		_reportServiceMock
			.Setup(s => s.GetAllAsync(null))
			.ReturnsAsync(Result.Ok<IEnumerable<ReportDto>>(reports));

		var result = await _controller.GetReports();

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task GetReports_FilterByPending_ReturnsPendingOnly()
	{
		SetUser("Admin");
		var pendingReports = new List<ReportDto>
		{
			new() { Id = Guid.NewGuid(), Status = ReportStatus.Pending }
		};

		_reportServiceMock
			.Setup(s => s.GetAllAsync(ReportStatus.Pending))
			.ReturnsAsync(Result.Ok<IEnumerable<ReportDto>>(pendingReports));

		var result = await _controller.GetReports(ReportStatus.Pending);

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task GetReportById_Exists_ReturnsReport()
	{
		SetUser("Moderator");
		var reportId = Guid.NewGuid();
		var report = new ReportDto
		{
			Id = reportId,
			TargetId = Guid.NewGuid(),
			TargetType = ReportableEntityType.Review,
			Status = ReportStatus.Pending
		};

		_reportServiceMock
			.Setup(s => s.GetByIdAsync(reportId))
			.ReturnsAsync(Result.Ok(report));

		var result = await _controller.GetReportById(reportId);

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task GetReportById_NotFound_Returns404()
	{
		SetUser("Moderator");
		var reportId = Guid.NewGuid();

		_reportServiceMock
			.Setup(s => s.GetByIdAsync(reportId))
			.ReturnsAsync(Result.Fail<ReportDto>("Report not found"));

		var result = await _controller.GetReportById(reportId);

		Assert.IsType<NotFoundObjectResult>(result);
	}

	[Fact]
	public async Task UpdateReport_Resolve_ReturnsOk()
	{
		SetUser("Moderator", "moderator-uuid-123");
		var reportId = Guid.NewGuid();
		var updateDto = new ReportDto
		{
			Id = reportId,
			TargetId = Guid.NewGuid(),
			TargetType = ReportableEntityType.Review,
			Status = ReportStatus.ResolvedDeleted,
			ProcessedByUserId = Guid.Parse("00000000-0000-0000-0000-000000000123")
		};

		_reportServiceMock
			.Setup(s => s.UpdateAsync(It.IsAny<ReportDto>()))
			.ReturnsAsync(Result.Ok(updateDto));

		var result = await _controller.UpdateReport(reportId, updateDto);

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task UpdateReport_Dismiss_ReturnsOk()
	{
		SetUser("Admin");
		var reportId = Guid.NewGuid();
		var updateDto = new ReportDto
		{
			Id = reportId,
			TargetId = Guid.NewGuid(),
			TargetType = ReportableEntityType.Comment,
			Status = ReportStatus.ResolvedDismissed,
			ProcessedByUserId = Guid.Parse(TestConstants.DefaultUserId)
		};

		_reportServiceMock
			.Setup(s => s.UpdateAsync(It.IsAny<ReportDto>()))
			.ReturnsAsync(Result.Ok(updateDto));

		var result = await _controller.UpdateReport(reportId, updateDto);

		Assert.IsType<OkObjectResult>(result);
		_reportServiceMock.Verify(s => s.UpdateAsync(
			It.Is<ReportDto>(r => r.Status == ReportStatus.ResolvedDismissed)), Times.Once);
	}

	[Fact]
	public async Task UpdateReport_IdMismatch_ReturnsBadRequest()
	{
		SetUser("Moderator");
		var routeId = Guid.NewGuid();
		var bodyId = Guid.NewGuid();

		var dto = new ReportDto { Id = bodyId, Status = ReportStatus.ResolvedDeleted };
		var result = await _controller.UpdateReport(routeId, dto);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task UpdateReport_NotFound_Returns404()
	{
		SetUser("Moderator");
		var reportId = Guid.NewGuid();
		var dto = new ReportDto { Id = reportId, Status = ReportStatus.ResolvedDeleted };

		_reportServiceMock
			.Setup(s => s.UpdateAsync(It.IsAny<ReportDto>()))
			.ReturnsAsync(Result.Fail<ReportDto>("Report not found"));

		var result = await _controller.UpdateReport(reportId, dto);

		Assert.IsType<NotFoundObjectResult>(result);
	}

	#endregion

	#region Delete report

	[Fact]
	public async Task DeleteReport_Exists_ReturnsNoContent()
	{
		SetUser("Admin");
		var reportId = Guid.NewGuid();

		_reportServiceMock
			.Setup(s => s.DeleteAsync(reportId))
			.ReturnsAsync(Result.Ok());

		var result = await _controller.DeleteReport(reportId);

		Assert.IsType<NoContentResult>(result);
	}

	[Fact]
	public async Task DeleteReport_NotFound_Returns404()
	{
		SetUser("Moderator");
		var reportId = Guid.NewGuid();

		_reportServiceMock
			.Setup(s => s.DeleteAsync(reportId))
			.ReturnsAsync(Result.Fail("Report not found"));

		var result = await _controller.DeleteReport(reportId);

		Assert.IsType<NotFoundObjectResult>(result);
	}

	#endregion

	#region US-602: Resolve report

	[Fact]
	public async Task ResolveReport_Delete_Returns204()
	{
		SetUser("Moderator");
		var reportId = Guid.NewGuid();
		var request = new ResolveReportRequest { Resolution = ReportStatus.ResolvedDeleted };

		_reportServiceMock
			.Setup(s => s.ResolveAsync(reportId, It.IsAny<Guid>(), It.IsAny<ResolveReportRequest>(), It.IsAny<bool>()))
			.ReturnsAsync(Result.Ok());

		var result = await _controller.ResolveReport(reportId, request);

		Assert.IsType<NoContentResult>(result);
	}

	[Fact]
	public async Task ResolveReport_Dismiss_Returns204()
	{
		SetUser("Admin");
		var reportId = Guid.NewGuid();
		var request = new ResolveReportRequest { Resolution = ReportStatus.ResolvedDismissed };

		_reportServiceMock
			.Setup(s => s.ResolveAsync(reportId, It.IsAny<Guid>(), It.IsAny<ResolveReportRequest>(), It.IsAny<bool>()))
			.ReturnsAsync(Result.Ok());

		var result = await _controller.ResolveReport(reportId, request);

		Assert.IsType<NoContentResult>(result);
	}

	[Fact]
	public async Task ResolveReport_AlreadyResolved_ReturnsBadRequest()
	{
		SetUser("Moderator");
		var reportId = Guid.NewGuid();
		var request = new ResolveReportRequest { Resolution = ReportStatus.ResolvedDeleted };

		_reportServiceMock
			.Setup(s => s.ResolveAsync(reportId, It.IsAny<Guid>(), It.IsAny<ResolveReportRequest>(), It.IsAny<bool>()))
			.ReturnsAsync(Result.Fail("Report is already resolved."));

		var result = await _controller.ResolveReport(reportId, request);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task ResolveReport_NoAuth_ReturnsBadRequest()
	{
		// No user claims set → GetUserId returns null
		var reportId = Guid.NewGuid();
		var request = new ResolveReportRequest { Resolution = ReportStatus.ResolvedDeleted };

		var result = await _controller.ResolveReport(reportId, request);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	#endregion

	#region Authorization attribute verification

	[Fact]
	public void ReportController_HasAuthorizeAttribute()
	{
		var controllerType = typeof(ReportController);
		var authAttr = controllerType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
		Assert.NotEmpty(authAttr);
	}

	[Fact]
	public void GetReports_RequiresAdminOrModerator()
	{
		var method = typeof(ReportController).GetMethod(nameof(ReportController.GetReports));
		Assert.NotNull(method);

		var authAttr = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
			.Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().FirstOrDefault();

		Assert.NotNull(authAttr);
		Assert.Contains("Admin", authAttr!.Roles!);
		Assert.Contains("Moderator", authAttr.Roles!);
	}

	[Fact]
	public void CreateReport_NoRoleRestriction_AnyAuthorizedUser()
	{
		var method = typeof(ReportController).GetMethod(nameof(ReportController.CreateReport));
		Assert.NotNull(method);

		// CreateReport should NOT have role-specific authorization (any authenticated user can report)
		var authAttrs = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
			.Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().ToList();

		// Either no method-level [Authorize] (inherits class-level) or no Roles restriction
		Assert.True(authAttrs.Count == 0 || authAttrs.All(a => string.IsNullOrEmpty(a.Roles)));
	}

	#endregion
}
