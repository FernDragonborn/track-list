namespace api.DTOs;

public class ExternalContentDto
{
	/// <summary>"ready" — data available; "loading" — fetch in progress, retry; "error" — last fetch failed.</summary>
	public string Status { get; set; } = "ready";

	public List<ExternalRatingDto> Ratings { get; set; } = new();
	public WikiReceptionDto? WikiReception { get; set; }
	public List<ExternalReviewDto> Reviews { get; set; } = new();

	/// <summary>Optional translations for the media description (TMDB synopsis). lang → translated text.</summary>
	public Dictionary<string, string>? DescriptionTranslations { get; set; }

	public DateTime? LastFetchedAt { get; set; }
	public DateTime? NextFetchDueAt { get; set; }
	public string? LastError { get; set; }
}

public class ExternalRatingDto
{
	public string Source { get; set; } = string.Empty;
	public double Score { get; set; }
	public string? RawScore { get; set; }
	public int? VoteCount { get; set; }
	public DateTime FetchedAt { get; set; }
}

public class WikiReceptionDto
{
	public Guid Id { get; set; }
	public string Content { get; set; } = string.Empty;
	public string? SourceUrl { get; set; }
	public DateTime FetchedAt { get; set; }

	/// <summary>Map of target lang code → translated text, populated when cached.</summary>
	public Dictionary<string, string>? Translations { get; set; }
}

public class ExternalReviewDto
{
	public Guid Id { get; set; }
	public string Source { get; set; } = string.Empty;
	public string? AuthorHandle { get; set; }
	public string? AuthorUrl { get; set; }
	public string Content { get; set; } = string.Empty;
	public int? Rating { get; set; }
	public int? LikeCountOnSource { get; set; }
	public string? SourceUrl { get; set; }
	public DateTime? PublishedAt { get; set; }
	public DateTime FetchedAt { get; set; }

	/// <summary>Map of target lang code → translated text, populated when cached (on-demand).</summary>
	public Dictionary<string, string>? Translations { get; set; }

	/// <summary>Virtual-profile metadata for the reviewer (avatar, display name, link-back).</summary>
	public ExternalReviewerDto? Reviewer { get; set; }
}

public class MediaRatingsBatchEntryDto
{
	public double? OurAvg { get; set; }
	public int OurCount { get; set; }
	public List<ExternalRatingDto> External { get; set; } = new();
}
