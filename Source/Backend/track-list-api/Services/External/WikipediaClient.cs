using System.Net;
using System.Text.Json;
using api.Utils;

namespace api.Services.External;

/// <summary>
///     Wikipedia client. Resolves film/series title to a Wikipedia article and pulls the
///     "Critical reception" section as plain text.
/// </summary>
public class WikipediaClient
{
	private readonly HttpClient _http;
	private readonly ILogger<WikipediaClient> _log;
	private readonly bool _enabled;

	public WikipediaClient(IHttpClientFactory httpFactory, ILogger<WikipediaClient> log)
	{
		_http = httpFactory.CreateClient(nameof(WikipediaClient));
		_http.Timeout = TimeSpan.FromSeconds(15);
		// Wikipedia best practices: include contact info in UA, identifiable bot string.
		_http.DefaultRequestHeaders.UserAgent.ParseAdd("TrackListEduBot/1.0 (https://github.com/FernDragonborn/track-list; tracklist-academic@example.com) dotnet/HttpClient");
		_log = log;
		_enabled = SelfHostSecurityOptions.ExternalServiceEnabled("TRACKLIST_ENABLE_WIKIPEDIA");
	}

	public record WikiSection(string Content, string ArticleUrl);

	public async Task<WikiSection?> FetchReceptionAsync(string title, int? year, string mediaType, CancellationToken ct)
	{
		if (!_enabled) return null;
		if (string.IsNullOrWhiteSpace(title)) return null;
		// Step 1: search article
		var pageTitle = await ResolveArticleTitleAsync(title, year, mediaType, ct);
		if (string.IsNullOrEmpty(pageTitle)) return null;

		// Step 2: fetch sections; find "Critical reception" / "Reception"
		var apiUrl = $"https://en.wikipedia.org/w/api.php?action=parse&page={Uri.EscapeDataString(pageTitle)}&prop=sections&format=json";
		string? json;
		try
		{
			json = await _http.GetStringAsync(apiUrl, ct);
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Wikipedia sections fetch failed: {Title}", pageTitle);
			return null;
		}

		int? receptionIndex = null;
		using (var doc = JsonDocument.Parse(json))
		{
			if (!doc.RootElement.TryGetProperty("parse", out var parse)) return null;
			if (!parse.TryGetProperty("sections", out var sections)) return null;
			foreach (var s in sections.EnumerateArray())
			{
				var line = s.TryGetProperty("line", out var l) ? l.GetString() : null;
				if (line is null) continue;
				if ((line.Equals("Critical reception", StringComparison.OrdinalIgnoreCase)
						|| line.Equals("Reception", StringComparison.OrdinalIgnoreCase)
						|| line.Equals("Critical response", StringComparison.OrdinalIgnoreCase))
					&& s.TryGetProperty("index", out var idx)
					&& int.TryParse(idx.GetString(), out var i))
				{
					receptionIndex = i;
					break;
				}
			}
		}
		if (receptionIndex is null) return null;

		// Step 3: fetch section text
		var textUrl = $"https://en.wikipedia.org/w/api.php?action=parse&page={Uri.EscapeDataString(pageTitle)}&prop=wikitext&section={receptionIndex}&format=json";
		try
		{
			var resp = await _http.GetStringAsync(textUrl, ct);
			using var doc = JsonDocument.Parse(resp);
			if (!doc.RootElement.TryGetProperty("parse", out var parse)) return null;
			if (!parse.TryGetProperty("wikitext", out var wt)) return null;
			var raw = wt.TryGetProperty("*", out var star) ? star.GetString() : null;
			if (string.IsNullOrWhiteSpace(raw)) return null;

			var clean = CleanWikitext(raw);
			if (clean.Length < 50) return null;
			if (clean.Length > 5000) clean = clean[..5000].TrimEnd() + "…";

			var articleUrl = $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(pageTitle)}";
			return new WikiSection(clean, articleUrl);
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Wikipedia section text fetch failed: {Title}", pageTitle);
			return null;
		}
	}

	private async Task<string?> ResolveArticleTitleAsync(string title, int? year, string mediaType, CancellationToken ct)
	{
		var suffix = mediaType.Equals("series", StringComparison.OrdinalIgnoreCase) ? " (TV series)" : " (film)";
		var candidates = new List<string>();
		if (year is not null) candidates.Add($"{title} ({year} {(suffix.Contains("film") ? "film" : "TV series")})");
		candidates.Add(title + suffix);
		candidates.Add(title);

		foreach (var c in candidates)
		{
			// redirects=1 → API returns the canonical title for redirect pages.
			var url = $"https://en.wikipedia.org/w/api.php?action=query&format=json&redirects=1&titles={Uri.EscapeDataString(c)}";
			try
			{
				var resp = await _http.GetStringAsync(url, ct);
				using var doc = JsonDocument.Parse(resp);
				if (!doc.RootElement.TryGetProperty("query", out var q)) continue;
				if (!q.TryGetProperty("pages", out var pages)) continue;
				foreach (var p in pages.EnumerateObject())
				{
					if (p.Value.TryGetProperty("missing", out _)) continue;
					if (p.Value.TryGetProperty("title", out var t)) return t.GetString();
				}
			}
			catch { /* try next candidate */ }
		}
		return null;
	}

	private static string CleanWikitext(string wt)
	{
		const System.Text.RegularExpressions.RegexOptions Single = System.Text.RegularExpressions.RegexOptions.Singleline;
		const System.Text.RegularExpressions.RegexOptions Multi = System.Text.RegularExpressions.RegexOptions.Multiline;

		// HTML comments
		wt = System.Text.RegularExpressions.Regex.Replace(wt, @"<!--.*?-->", "", Single);
		// Refs <ref>...</ref>, self-closing, named
		wt = System.Text.RegularExpressions.Regex.Replace(wt, @"<ref[^>]*?/>", "");
		wt = System.Text.RegularExpressions.Regex.Replace(wt, @"<ref[^>]*>.*?</ref>", "", Single);
		// gallery / timeline / score etc.
		wt = System.Text.RegularExpressions.Regex.Replace(wt, @"<(gallery|timeline|score|imagemap|syntaxhighlight|source|nowiki|math)[^>]*>.*?</\1>", "", Single);
		// Tables {| ... |} — iterate to handle nested
		for (int i = 0; i < 6 && wt.Contains("{|"); i++)
			wt = System.Text.RegularExpressions.Regex.Replace(wt, @"\{\|[^{}]*?\|\}", "", Single);
		// Templates {{...}} — iterate for nesting
		for (int i = 0; i < 8 && wt.Contains("{{"); i++)
			wt = System.Text.RegularExpressions.Regex.Replace(wt, @"\{\{[^{}]*\}\}", "");
		// File/Image links [[File:...]] or [[Image:...]] — including nested formatting
		wt = System.Text.RegularExpressions.Regex.Replace(wt, @"\[\[(?:File|Image):[^\[\]]*(?:\[\[[^\]]*\]\][^\[\]]*)*\]\]", "", Single);
		// Section headings ==..==
		wt = System.Text.RegularExpressions.Regex.Replace(wt, @"={2,}\s*[^=\n]+\s*={2,}", "");
		// Wiki links [[Article|display]] → display; [[Article]] → Article
		wt = System.Text.RegularExpressions.Regex.Replace(wt, @"\[\[([^\]\|]+)\|([^\]]+)\]\]", "$2");
		wt = System.Text.RegularExpressions.Regex.Replace(wt, @"\[\[([^\]]+)\]\]", "$1");
		// External links [url label] → label; [url] → drop
		wt = System.Text.RegularExpressions.Regex.Replace(wt, @"\[https?://[^\s\]]+\s+([^\]]+)\]", "$1");
		wt = System.Text.RegularExpressions.Regex.Replace(wt, @"\[https?://[^\s\]]+\]", "");
		// '''bold''' / ''italic''
		wt = System.Text.RegularExpressions.Regex.Replace(wt, @"'''([^']+)'''", "$1");
		wt = System.Text.RegularExpressions.Regex.Replace(wt, @"''([^']+)''", "$1");
		// Lists/indents at line start (*, #, ;, :, |, !)
		wt = System.Text.RegularExpressions.Regex.Replace(wt, @"^[\*#;:\|!]+\s*", "", Multi);
		// HTML tags
		wt = System.Text.RegularExpressions.Regex.Replace(wt, @"<[^>]+>", "");
		// HTML entities
		wt = WebUtility.HtmlDecode(wt);
		// Collapse whitespace
		wt = System.Text.RegularExpressions.Regex.Replace(wt, @"[ \t]+", " ");
		wt = System.Text.RegularExpressions.Regex.Replace(wt, @"\n{3,}", "\n\n");
		wt = wt.Trim();
		return wt;
	}
}
