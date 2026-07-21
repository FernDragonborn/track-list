using System.Globalization;
using System.Text.Json;
using api.Utils;
using dotenv.net;

namespace api.Services.External;

/// <summary>
///     OMDb API client. Fetches IMDb rating + cross-source ratings (RT, Metacritic) by IMDb ID.
///     Free key (1000/day) — required in OMDB_API_KEY env var.
/// </summary>
public class OmdbClient
{
	private readonly HttpClient _http;
	private readonly ILogger<OmdbClient> _log;
	private readonly string? _apiKey;
	private readonly bool _enabled;

	public OmdbClient(IHttpClientFactory httpFactory, ILogger<OmdbClient> log)
	{
		_http = httpFactory.CreateClient(nameof(OmdbClient));
		_http.Timeout = TimeSpan.FromSeconds(10);
		_log = log;
		var env = DotEnv.Read();
		_enabled = SelfHostSecurityOptions.ExternalServiceEnabled("TRACKLIST_ENABLE_OMDB");
		_apiKey = Environment.GetEnvironmentVariable("OMDB_API_KEY") ?? (env.TryGetValue("OMDB_API_KEY", out var k) ? k : null);
	}

	public bool IsConfigured => _enabled && !string.IsNullOrWhiteSpace(_apiKey);

	/// <summary>Fetch aggregate ratings for a media by IMDb tt-id (e.g. "tt0137523"). Returns null on failure.</summary>
	public async Task<List<OmdbRating>?> FetchAsync(string imdbId, CancellationToken ct)
	{
		if (!IsConfigured)
		{
			_log.LogInformation("OMDb integration is disabled or not configured");
			return null;
		}
		if (string.IsNullOrWhiteSpace(imdbId)) return null;
		// IMDb IDs always start "tt" + digits
		if (!imdbId.StartsWith("tt", StringComparison.OrdinalIgnoreCase)) return null;

		var url = $"https://www.omdbapi.com/?i={Uri.EscapeDataString(imdbId)}&apikey={_apiKey}&tomatoes=true";
		try
		{
			using var resp = await _http.GetAsync(url, ct);
			if (!resp.IsSuccessStatusCode)
			{
				_log.LogWarning("OMDb HTTP {Status} for {ImdbId}", (int)resp.StatusCode, imdbId);
				return null;
			}
			var body = await resp.Content.ReadAsStringAsync(ct);
			using var doc = JsonDocument.Parse(body);
			var root = doc.RootElement;
			if (root.TryGetProperty("Response", out var r) && r.ValueKind == JsonValueKind.String && r.GetString() == "False")
			{
				return null;
			}

			var ratings = new List<OmdbRating>();

			// imdbRating + imdbVotes top-level
			if (root.TryGetProperty("imdbRating", out var imdbR) && imdbR.ValueKind == JsonValueKind.String
			    && double.TryParse(imdbR.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var imdbScore))
			{
				int? votes = null;
				if (root.TryGetProperty("imdbVotes", out var imdbV) && imdbV.ValueKind == JsonValueKind.String)
				{
					var v = imdbV.GetString()?.Replace(",", "") ?? "";
					if (int.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var iv)) votes = iv;
				}
				ratings.Add(new OmdbRating("imdb", imdbScore, imdbR.GetString() + "/10", votes));
			}

			// Ratings[] array: RT (e.g. "92%"), Metacritic (e.g. "67/100"), Internet Movie Database (already taken)
			if (root.TryGetProperty("Ratings", out var arr) && arr.ValueKind == JsonValueKind.Array)
			{
				foreach (var item in arr.EnumerateArray())
				{
					var src = item.TryGetProperty("Source", out var s) ? s.GetString() : null;
					var val = item.TryGetProperty("Value", out var v) ? v.GetString() : null;
					if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(val)) continue;

					if (src.Equals("Rotten Tomatoes", StringComparison.OrdinalIgnoreCase))
					{
						var pct = val.TrimEnd('%');
						if (double.TryParse(pct, NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
							ratings.Add(new OmdbRating("rotten_tomatoes", p / 10.0, val, null));
					}
					else if (src.Equals("Metacritic", StringComparison.OrdinalIgnoreCase))
					{
						var slash = val.IndexOf('/');
						var n = slash > 0 ? val[..slash] : val;
						if (double.TryParse(n, NumberStyles.Any, CultureInfo.InvariantCulture, out var m))
							ratings.Add(new OmdbRating("metacritic", m / 10.0, val, null));
					}
					// Internet Movie Database covered by imdbRating above — skip.
				}
			}

			return ratings;
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "OMDb fetch failed for {ImdbId}", imdbId);
			return null;
		}
	}

	public record OmdbRating(string Source, double Score, string RawScore, int? VoteCount);
}
