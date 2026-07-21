using api.DTOs;
using api.Models;
using api.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

/// <summary>
///     Read-only endpoints for external reviewer "virtual profiles" — these are NOT TrackList
///     users, just denormalized metadata about external critics whose reviews we've cached.
///     No auth required; all data is already public on the source.
/// </summary>
[Route("api/external-reviewers")]
[ApiController]
public sealed class ExternalReviewerController(IExternalReviewerService reviewers) : ControllerBase
{
	private const int DefaultRecentLimit = 10;
	private const int MaxPageSize = 50;

	/// <summary>Reviewer profile + counts + recent reviews. 404 if reviewer unknown.</summary>
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[HttpGet("{handle}")]
	public async Task<ObjectResult> GetByHandle(
		string handle,
		[FromQuery] string source = "letterboxd",
		[FromQuery] int recent = DefaultRecentLimit,
		CancellationToken ct = default)
	{
		recent = Math.Clamp(recent, 1, 50);
		var profile = await reviewers.GetProfileAsync(source, handle, recent, ct);
		if (profile is null) return NotFound(new { error = "External reviewer not found" });

		var dto = ExternalReviewerMappings.ToProfileDto(profile);
		return Ok(new { data = dto });
	}

	/// <summary>Paginated reviews for one reviewer.</summary>
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[HttpGet("{handle}/reviews")]
	public async Task<ObjectResult> GetReviews(
		string handle,
		[FromQuery] string source = "letterboxd",
		[FromQuery] string? cursor = null,
		[FromQuery] int limit = 20,
		CancellationToken ct = default)
	{
		limit = Math.Clamp(limit, 1, MaxPageSize);
		var (items, nextCursor) = await reviewers.GetReviewsAsync(source, handle, cursor, limit, ct);
		if (items.Count == 0 && cursor is null)
		{
			// Disambiguate "unknown reviewer" from "no reviews yet" — re-check profile.
			var prof = await reviewers.GetProfileAsync(source, handle, 1, ct);
			if (prof is null) return NotFound(new { error = "External reviewer not found" });
		}

		return Ok(new
		{
			data = new CursorPagedResultDto<ExternalReviewWithMediaDto>
			{
				Items = items.Select(ExternalReviewerMappings.ToReviewDto).ToList(),
				NextCursor = nextCursor,
			}
		});
	}
}

[Route("api/external-feed")]
[ApiController]
public sealed class ExternalFeedController(IExternalReviewerService reviewers) : ControllerBase
{
	private const int MaxPageSize = 50;

	/// <summary>Global chronological feed of external reviews across all reviewers.</summary>
	[ProducesResponseType(StatusCodes.Status200OK)]
	[HttpGet]
	public async Task<ObjectResult> GetFeed(
		[FromQuery] string? cursor = null,
		[FromQuery] int limit = 20,
		CancellationToken ct = default)
	{
		limit = Math.Clamp(limit, 1, MaxPageSize);
		var (items, nextCursor) = await reviewers.GetGlobalFeedAsync(cursor, limit, ct);
		return Ok(new
		{
			data = new CursorPagedResultDto<ExternalReviewFeedItemDto>
			{
				Items = items.Select(ExternalReviewerMappings.ToFeedItemDto).ToList(),
				NextCursor = nextCursor,
			}
		});
	}
}

/// <summary>Mapping helpers — kept here next to the only two controllers that need them.</summary>
internal static class ExternalReviewerMappings
{
	private static int LanguagePriority(string? code) => code switch
	{
		"uk" => 0,
		"en" => 1,
		_ => 2,
	};

	public static ExternalReviewerProfileDto ToProfileDto(ExternalReviewerProfile profile) => new()
	{
		Id = profile.Reviewer.Id,
		Source = profile.Reviewer.Source,
		Handle = profile.Reviewer.Handle,
		DisplayName = profile.Reviewer.DisplayName,
		Bio = profile.Reviewer.Bio,
		AvatarUrl = profile.Reviewer.AvatarUrl,
		SourceProfileUrl = profile.Reviewer.SourceProfileUrl,
		LastSyncedAt = profile.Reviewer.LastSyncedAt,
		ReviewCount = profile.ReviewCount,
		AverageRating = profile.AverageRating,
		RecentReviews = profile.RecentReviews.Select(ToReviewDto).ToList(),
	};

	public static ExternalReviewerDto ToDto(ExternalReviewer r) => new()
	{
		Id = r.Id,
		Source = r.Source,
		Handle = r.Handle,
		DisplayName = r.DisplayName,
		Bio = r.Bio,
		AvatarUrl = r.AvatarUrl,
		SourceProfileUrl = r.SourceProfileUrl,
		LastSyncedAt = r.LastSyncedAt,
	};

	public static ExternalReviewWithMediaDto ToReviewDto(ExternalReview review)
	{
		var title = review.Media?.Translations?
			.OrderBy(t => LanguagePriority(t.LanguageCode))
			.Select(t => t.Title)
			.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
		return new ExternalReviewWithMediaDto
		{
			Id = review.Id,
			MediaId = review.MediaId,
			MediaTitle = title,
			MediaReleaseYear = review.Media?.ReleaseYear,
			MediaPosterUrl = review.Media?.PosterUrl,
			Source = review.Source,
			AuthorHandle = review.AuthorHandle,
			AuthorUrl = review.AuthorUrl,
			Content = review.Content,
			Rating = review.Rating,
			SourceUrl = review.SourceUrl,
			PublishedAt = review.PublishedAt,
			FetchedAt = review.FetchedAt,
		};
	}

	public static ExternalReviewFeedItemDto ToFeedItemDto(ExternalReview review)
	{
		var basic = ToReviewDto(review);
		return new ExternalReviewFeedItemDto
		{
			Id = basic.Id,
			MediaId = basic.MediaId,
			MediaTitle = basic.MediaTitle,
			MediaReleaseYear = basic.MediaReleaseYear,
			MediaPosterUrl = basic.MediaPosterUrl,
			Source = basic.Source,
			AuthorHandle = basic.AuthorHandle,
			AuthorUrl = basic.AuthorUrl,
			Content = basic.Content,
			Rating = basic.Rating,
			SourceUrl = basic.SourceUrl,
			PublishedAt = basic.PublishedAt,
			FetchedAt = basic.FetchedAt,
			Reviewer = review.ExternalReviewer is null ? null : ToDto(review.ExternalReviewer),
		};
	}
}

