using System.Security.Claims;
using api.DTOs;
using api.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace api.Controllers;

[Authorize]
[Route("api/report")]
[ApiController]
public class ReportController(IReportService reportService) : ControllerBase
{
	/// <summary>
	///     Get specific report by ID
	/// </summary>
	/// <param name="id">Report GUID</param>
	/// <returns>Report DTO</returns>
	[Authorize(Roles = "Admin,Moderator")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[HttpGet("{id:guid}")]
	public async Task<ObjectResult> GetReportById(Guid id)
	{
		var result = await reportService.GetByIdAsync(id);

		if (result.IsSuccess)
			return Ok(new { data = result.Value });

		return NotFound(new { error = result.Error });
	}

	/// <summary>
	///     Get all reports (optional filtering by status)
	/// </summary>
	/// <param name="reportStatus">Filter by status (Pending, Resolved, etc.)</param>
	/// <returns>List of reports</returns>
	[Authorize(Roles = "Admin,Moderator")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[HttpGet]
	public async Task<ObjectResult> GetReports([FromQuery] ReportStatus? reportStatus = null)
	{
		var result = await reportService.GetAllAsync(reportStatus);

		// Тут завжди Success, навіть якщо список порожній (поверне порожній масив)
		return Ok(new { data = result.Value });
	}

	/// <summary>
	///     Create a new report
	/// </summary>
	/// <param name="reportDto">Report data</param>
	/// <returns>Created report</returns>
	[ProducesResponseType(StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[EnableRateLimiting("write")]
	[HttpPost]
	public async Task<ObjectResult> CreateReport([FromBody] ReportDto reportDto)
	{
		var reporterId = GetUserId();
		if (reporterId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

		reportDto = reportDto with { ReporterId = reporterId.Value };
		ModelState.Remove(nameof(reportDto.ReporterId));

		if (!ModelState.IsValid)
			return BadRequest(new { error = "Validation failed", details = ModelState });

		var result = await reportService.CreateAsync(reportDto);

		if (result.IsFailure)
			return BadRequest(new { error = result.Error });

		return CreatedAtAction(
			nameof(GetReportById),
			new { id = result.Value.Id },
			new { data = result.Value });
	}

	/// <summary>
	///     Update an existing report
	/// </summary>
	/// <param name="id">Report ID from route</param>
	/// <param name="reportDto">Updated report data</param>
	/// <returns>Updated report</returns>
	[Authorize(Roles = "Admin,Moderator")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[HttpPut("{id:guid}")]
	public async Task<ObjectResult> UpdateReport(Guid id, [FromBody] ReportDto reportDto)
	{
		if (id != reportDto.Id)
			return BadRequest(new { error = "ID mismatch between route and body." });

		if (!ModelState.IsValid)
			return BadRequest(new { error = "Validation failed", details = ModelState });

		var result = await reportService.UpdateAsync(reportDto);

		if (result.IsFailure)
		{
			// Якщо не знайдено - 404, інакше 400. 
			// Для спрощення можна завжди повертати BadRequest або NotFound залежно від тексту помилки,
			// але тут проста логіка:
			if (result.Error.Contains("not found"))
				return NotFound(new { error = result.Error });

			return BadRequest(new { error = result.Error });
		}

		return Ok(new { data = result.Value });
	}

	/// <summary>
	///     Delete a report
	/// </summary>
	/// <param name="id">Report ID</param>
	/// <returns>No Content</returns>
	[Authorize(Roles = "Admin,Moderator")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[HttpDelete("{id:guid}")]
	public async Task<IActionResult> DeleteReport(Guid id)
	{
		var result = await reportService.DeleteAsync(id);

		if (result.IsFailure)
			return NotFound(new { error = result.Error });

		return NoContent();
	}

	/// <summary>
	///     Resolve a pending report (delete target content or dismiss).
	/// </summary>
	/// <param name="id">Report ID</param>
	/// <param name="request">Resolution action</param>
	/// <returns>No Content</returns>
	[Authorize(Roles = "Admin,Moderator")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
	[HttpPost("{id:guid}/resolve")]
	public async Task<IActionResult> ResolveReport(Guid id, [FromBody] ResolveReportRequest request)
	{
		var moderatorId = GetUserId();
		if (moderatorId is null) return BadRequest(new { error = "User ID claim is missing or invalid." });

		var isAdmin = User.IsInRole("Admin");
		var result = await reportService.ResolveAsync(id, moderatorId.Value, request, isAdmin);

		if (result.IsFailure)
			return BadRequest(new { error = result.Error });

		return NoContent();
	}

	private Guid? GetUserId()
	{
		var idStr = User.FindFirstValue("id")
		            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

		return Guid.TryParse(idStr, out var guid) ? guid : null;
	}
}
