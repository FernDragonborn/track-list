using api.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[Route("api/stats")]
[ApiController]
public sealed class StatsController(IPublicStatsService statsService) : ControllerBase
{
	/// <summary>Anonymous, cached aggregate stats for the About page.</summary>
	[ProducesResponseType(StatusCodes.Status200OK)]
	[HttpGet("public")]
	public async Task<ObjectResult> GetPublicStats(CancellationToken ct)
	{
		var stats = await statsService.GetAsync(ct);
		return Ok(new { data = stats });
	}
}
