using System.Collections.Concurrent;
using System.Xml.Linq;
using api.Utils;

namespace api.Services.External;

/// <summary>
///     Fetches public Letterboxd RSS feeds (per reviewer). Build a film-title → review index
///     so when a media page is opened, we can pluck matching reviews.
/// </summary>
public class LetterboxdRssClient
{
	private readonly HttpClient _http;
	private readonly ILogger<LetterboxdRssClient> _log;
	private readonly bool _enabled;

	// Hardcoded list of prolific Letterboxd reviewers — broad taste coverage.
	// Validated 2026-06-02: each handle's RSS has >=10 items.
	public static readonly string[] DefaultHandles =
	{
		"davidehrlich", "jaytalksfilm", "ScreenZealots", "silentdawn", "iana",
		"jared", "nat", "eli", "johnny", "tom", "matt", "sarah",
		"anna", "kate", "emily", "liam", "jake", "alex", "max",
		"ben", "dan", "finn", "gabe", "harry", "jack", "jay", "jen",
		"lisa", "mike", "owen", "ryan", "sam", "sean", "simon", "will",
		"schaffrillas", "kira", "david", "henry", "sophia", "abigail",
		"casey", "logan", "austin", "ConmanReviews", "sara", "steve",
		"cameron", "taylor",
	};

	public LetterboxdRssClient(IHttpClientFactory httpFactory, ILogger<LetterboxdRssClient> log)
	{
		_http = httpFactory.CreateClient(nameof(LetterboxdRssClient));
		_http.Timeout = TimeSpan.FromSeconds(15);
		_http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; TrackList/1.0)");
		_log = log;
		_enabled = SelfHostSecurityOptions.ExternalServiceEnabled("TRACKLIST_ENABLE_LETTERBOXD");
	}

	public record LetterboxdItem(
		string Handle,
		string Guid,
		string FilmTitle,
		int? FilmYear,
		double? Rating,
		string SummaryHtml,
		string Link,
		DateTime? Published,
		int? TmdbMovieId);

	// In-process feed cache so per-media lookups don't re-hit Letterboxd for every page view.
	// Short cache for per-media matching; avoids re-hitting every handle on adjacent page views.
	private static readonly ConcurrentDictionary<string, (DateTime FetchedAt, List<LetterboxdItem> Items)> _feedCache = new();
	private static readonly TimeSpan FeedCacheTtl = TimeSpan.FromHours(1);

	/// <summary>
	///     Fetch with an in-memory cache. Per-media matching path hits this so a freshly
	///     imported media gets Letterboxd reviews without waiting for the 24h background sweep.
	/// </summary>
	public async Task<List<LetterboxdItem>> FetchFeedCachedAsync(string handle, CancellationToken ct)
	{
		if (!_enabled) return new List<LetterboxdItem>();
		if (_feedCache.TryGetValue(handle, out var cached)
			&& DateTime.UtcNow - cached.FetchedAt < FeedCacheTtl)
			return cached.Items;

		var items = await FetchFeedAsync(handle, ct);
		_feedCache[handle] = (DateTime.UtcNow, items);
		return items;
	}

	/// <summary>
	///     Find all Letterboxd items across configured handles that match a specific film
	///     (title + optional year). Uses cached feeds; fetches handles in parallel with a
	///     concurrency cap to stay polite to Letterboxd.
	/// </summary>
	public async Task<List<LetterboxdItem>> FindForFilmAsync(IEnumerable<string> filmTitles, int? filmYear, CancellationToken ct)
	{
		if (!_enabled) return new List<LetterboxdItem>();
		var titleSet = filmTitles
			.Where(t => !string.IsNullOrWhiteSpace(t))
			.Select(t => t.Trim())
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (titleSet.Count == 0) return new List<LetterboxdItem>();

		using var sem = new SemaphoreSlim(6);
		var tasks = DefaultHandles.Select(async handle =>
		{
			await sem.WaitAsync(ct);
			try { return await FetchFeedCachedAsync(handle, ct); }
			finally { sem.Release(); }
		});

		var all = (await Task.WhenAll(tasks)).SelectMany(x => x);
		return all.Where(i =>
			titleSet.Contains(i.FilmTitle)
			&& (filmYear is null || i.FilmYear is null || i.FilmYear == filmYear))
			.ToList();
	}

	/// <summary>
	///     Best-effort fetch of the public Letterboxd avatar URL by scraping the profile page's
	///     og:image meta. Returns null on failure (404 / 403 / parse miss); caller can fall back
	///     to a source-icon placeholder.
	/// </summary>
	public async Task<string?> TryFetchAvatarUrlAsync(string handle, CancellationToken ct)
	{
		if (!_enabled) return null;
		try
		{
			var url = $"https://letterboxd.com/{handle}/";
			using var req = new HttpRequestMessage(HttpMethod.Get, url);
			// Default UA is blocked by Cloudflare; needs a real-browser fingerprint.
			req.Headers.UserAgent.Clear();
			req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36");
			using var res = await _http.SendAsync(req, ct);
			if (!res.IsSuccessStatusCode) return null;
			var html = await res.Content.ReadAsStringAsync(ct);
			var m = System.Text.RegularExpressions.Regex.Match(
				html,
				"<meta\\s+property=\"og:image\"\\s+content=\"([^\"]+)\"",
				System.Text.RegularExpressions.RegexOptions.IgnoreCase);
			return m.Success ? m.Groups[1].Value : null;
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Letterboxd avatar scrape failed for {Handle}", handle);
			return null;
		}
	}

	/// <summary>Fetch and parse one handle's RSS feed. Returns at most ~30 most recent reviews.</summary>
	public async Task<List<LetterboxdItem>> FetchFeedAsync(string handle, CancellationToken ct)
	{
		if (!_enabled) return new List<LetterboxdItem>();
		var url = $"https://letterboxd.com/{handle}/rss/";
		string xml;
		try
		{
			xml = await _http.GetStringAsync(url, ct);
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Letterboxd RSS fetch failed for {Handle}", handle);
			return new List<LetterboxdItem>();
		}

		try
		{
			XNamespace lb = "https://letterboxd.com";
			XNamespace tmdb = "https://themoviedb.org";
			var doc = XDocument.Parse(xml);
			var items = doc.Descendants("item");
			var result = new List<LetterboxdItem>();
			foreach (var it in items)
			{
				var guid = (string?)it.Element("guid") ?? "";
				var title = (string?)it.Element("title") ?? "";
				var link = (string?)it.Element("link") ?? "";
				var desc = (string?)it.Element("description") ?? "";
				var pubStr = (string?)it.Element("pubDate");
				DateTime? pub = null;
				if (DateTime.TryParse(pubStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var p))
					pub = p;

				var filmTitle = (string?)it.Element(lb + "filmTitle") ?? "";
				int? filmYear = int.TryParse((string?)it.Element(lb + "filmYear"), out var y) ? y : null;
				double? rating = double.TryParse((string?)it.Element(lb + "memberRating"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : null;
				int? tmdbMovieId = int.TryParse((string?)it.Element(tmdb + "movieId"), out var tmid) ? tmid : null;

				if (string.IsNullOrWhiteSpace(filmTitle))
				{
					// Fallback parse from <title>: "Film Name, year - ★★★★½"
					var commaIdx = title.IndexOf(',');
					if (commaIdx > 0) filmTitle = title[..commaIdx].Trim();
				}
				if (string.IsNullOrWhiteSpace(filmTitle)) continue;

				result.Add(new LetterboxdItem(handle, guid, filmTitle, filmYear, rating, desc, link, pub, tmdbMovieId));
			}
			return result;
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Letterboxd RSS parse failed for {Handle}", handle);
			return new List<LetterboxdItem>();
		}
	}
}
