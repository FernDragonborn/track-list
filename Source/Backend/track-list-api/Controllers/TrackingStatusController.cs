using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class TrackingStatusController : ControllerBase
	{
		private readonly IUnitOfWork _unitOfWork;
		public TrackingStatusController(IUnitOfWork unitOfWork)
		{
			_unitOfWork = unitOfWork;
		}

		/// <summary>
		///     Add or update tracking status for a media item for the current user.
		///     If the status already exists, it will be updated; otherwise, a new status will be created.
		/// </summary>
		/// <param name="trackingStatusDto">Tracking status dto</param>
		/// <returns>Created or updated tracking status</returns>
		[ProducesResponseType(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[Authorize]
		[HttpPost]
		public async Task<ObjectResult> UpsertTrackingStatus([FromBody] TrackingStatusDto trackingStatusDto)
		{
			var idClaim = User.Claims.FirstOrDefault(claim => claim.Type == "id");
			if (idClaim == null || !Guid.TryParse(idClaim.Value, out var userId))
			{
				return BadRequest(new { error = "User ID claim is missing or invalid." });
			}

			var existingStatus = await _unitOfWork.TrackingStatusRepository
				.GetOneAsync(ts => ts.UserId == userId && ts.MediaId == trackingStatusDto.MediaId);

			Result<TrackingStatus> result;

			if (existingStatus is { IsSuccess: true, Value: not null })
			{
				var existingStatusValue = existingStatus.Value;
				existingStatusValue.Status = trackingStatusDto.Status;

				if (trackingStatusDto.Progress.HasValue)
				{
					existingStatusValue.Progress = trackingStatusDto.Progress;
				}

				result = Result.Ok(await _unitOfWork.TrackingStatusRepository.Update(existingStatusValue));
			}
			else
			{
				var newStatus = new TrackingStatus
				{
					UserId = userId,
					MediaId = trackingStatusDto.MediaId,
					Status = trackingStatusDto.Status,
				};

				if (trackingStatusDto.Progress.HasValue)
				{
					newStatus.Progress = trackingStatusDto.Progress;
				}

				result = await _unitOfWork.TrackingStatusRepository.AddAsync(newStatus);
			}
			await _unitOfWork.SaveAsync();
			return Ok(new { data = result.Value });
		}

		/// <summary>
		///     Get tracking status for a specific media item for the current user.
		/// </summary>
		/// <param name="mediaId">Media ID (GUID)</param>
		/// <returns>Tracking status or null</returns>
		[ProducesResponseType(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[HttpGet("{mediaId:guid}")]
		[Authorize]
		public async Task<ObjectResult> GetTrackingStatus(Guid mediaId)
		{
			var idClaim = User.Claims.FirstOrDefault(claim => claim.Type == "id");
			if (idClaim == null || !Guid.TryParse(idClaim.Value, out var userId))
			{
				return BadRequest(new { error = "User ID claim is missing or invalid." });
			}

			var trackingStatusResult = await _unitOfWork.TrackingStatusRepository
				.GetOneAsync(ts => ts.UserId == userId && ts.MediaId == mediaId);

			if (trackingStatusResult.IsSuccess)
			{
				return Ok(new { data = trackingStatusResult.Value });
			}

			return Ok(new { data = (TrackingStatus?)null });
		}

		/// <summary>
		///     Get all tracking statuses for the current user.
		/// </summary>
		/// <returns>List of tracking statuses</returns>
		[ProducesResponseType(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[HttpGet]
		[Authorize]
		public async Task<ObjectResult> GetTrackingStatuses()
		{
			var idClaim = User.Claims.FirstOrDefault(claim => claim.Type == "id");
			if (idClaim == null || !Guid.TryParse(idClaim.Value, out var userId))
			{
				return BadRequest(new { error = "User ID claim is missing or invalid." });
			}

			var trackingStatusesResult = await _unitOfWork.TrackingStatusRepository
				.GetAsync(ts => ts.UserId == userId);

			if (trackingStatusesResult.IsSuccess)
			{
				return Ok(new { data = trackingStatusesResult.Value });
			}
			return Ok(new { data = new List<TrackingStatus>() });
		}

		/// <summary>
		///     Get tracking statistics (counts per status) for a specific media item.
		/// </summary>
		/// <param name="mediaId">Media ID (GUID)</param>
		/// <returns>Counts per tracking status</returns>
		[ProducesResponseType(StatusCodes.Status200OK)]
		[HttpGet("media/{mediaId:guid}/stats")]
		public async Task<ObjectResult> GetMediaTrackingStats(Guid mediaId)
		{
			var result = await _unitOfWork.TrackingStatusRepository
				.GetAsync(ts => ts.MediaId == mediaId);

			if (!result.IsSuccess)
				return Ok(new { data = new { planned = 0, watching = 0, completed = 0, dropped = 0 } });

			var statuses = result.Value;
			return Ok(new
			{
				data = new
				{
					planned   = statuses.Count(ts => ts.Status == TrackingStatusCode.Planned),
					watching  = statuses.Count(ts => ts.Status == TrackingStatusCode.Watching),
					completed = statuses.Count(ts => ts.Status == TrackingStatusCode.Completed),
					dropped   = statuses.Count(ts => ts.Status == TrackingStatusCode.Dropped),
				}
			});
		}

		/// <summary>
		///     Delete tracking status for a media item for the current user.
		/// </summary>
		/// <param name="mediaId">Media ID (GUID)</param>
		/// <returns>No Content</returns>
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[Authorize]
		[HttpDelete("{mediaId:guid}")]
		public async Task<IActionResult> DeleteTrackingStatus(Guid mediaId)
		{
			var idClaim = User.Claims.FirstOrDefault(claim => claim.Type == "id");
			if (idClaim == null || !Guid.TryParse(idClaim.Value, out var userId))
			{
				return BadRequest(new { error = "User ID claim is missing or invalid." });
			}

			var existingStatusResult = await _unitOfWork.TrackingStatusRepository
				.GetOneAsync(ts => ts.UserId == userId && ts.MediaId == mediaId);

			if (!existingStatusResult.IsSuccess)
			{
				return BadRequest(new { error = "Tracking status not found for the specified media ID." });
			}

			var result = await _unitOfWork.TrackingStatusRepository.Remove(existingStatusResult.Value);

			if (result.IsFailure)
				return BadRequest(new { error = result.Error });

			await _unitOfWork.SaveAsync();
			return NoContent();
		}
	}
}
