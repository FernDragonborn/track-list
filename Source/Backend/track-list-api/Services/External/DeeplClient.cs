using System.Text;
using System.Text.Json;
using api.Utils;
using dotenv.net;

namespace api.Services.External;

/// <summary>
///     DeepL API client. Free key (suffix ":fx") routes to api-free.deepl.com; paid → api.deepl.com.
/// </summary>
public class DeeplClient
{
	private readonly HttpClient _http;
	private readonly ILogger<DeeplClient> _log;
	private readonly string? _key;
	private readonly string _endpoint;
	private readonly bool _enabled;

	public DeeplClient(IHttpClientFactory httpFactory, ILogger<DeeplClient> log)
	{
		_http = httpFactory.CreateClient(nameof(DeeplClient));
		_http.Timeout = TimeSpan.FromSeconds(30);
		_log = log;
		var env = DotEnv.Read();
		_enabled = SelfHostSecurityOptions.ExternalServiceEnabled("TRACKLIST_ENABLE_DEEPL");
		_key = Environment.GetEnvironmentVariable("DEEPL_API_KEY") ?? (env.TryGetValue("DEEPL_API_KEY", out var k) ? k : null);
		_endpoint = (_key?.EndsWith(":fx", StringComparison.Ordinal) ?? false)
			? "https://api-free.deepl.com/v2/translate"
			: "https://api.deepl.com/v2/translate";
	}

	public bool IsConfigured => _enabled && !string.IsNullOrWhiteSpace(_key);

	public record TranslationResult(string Text, string SourceLang);

	/// <summary>
	///     Translate a single string. targetLang is ISO 639-1 (e.g. "uk", "en"). Returns null on failure.
	/// </summary>
	public async Task<TranslationResult?> TranslateAsync(string text, string targetLang, CancellationToken ct)
	{
		if (!IsConfigured)
		{
			_log.LogInformation("DeepL integration is disabled or not configured");
			return null;
		}
		if (string.IsNullOrWhiteSpace(text)) return null;

		var payload = JsonSerializer.Serialize(new
		{
			text = new[] { text },
			target_lang = targetLang.ToUpperInvariant(),
		});

		using var req = new HttpRequestMessage(HttpMethod.Post, _endpoint)
		{
			Content = new StringContent(payload, Encoding.UTF8, "application/json"),
		};
		req.Headers.TryAddWithoutValidation("Authorization", $"DeepL-Auth-Key {_key}");

		try
		{
			using var resp = await _http.SendAsync(req, ct);
			if (!resp.IsSuccessStatusCode)
			{
				var status = (int)resp.StatusCode;
				// DeepL-specific: 456 = monthly character quota exceeded; 429 = rate limit; 403 = bad/inactive key.
				if (status == 456)
					_log.LogError("DeepL QUOTA EXCEEDED (HTTP 456) — monthly character limit reached. Translations disabled until quota resets or a new key is provided. target={Lang}", targetLang);
				else if (status == 429)
					_log.LogWarning("DeepL rate limit (HTTP 429) for target={Lang} — back off and retry", targetLang);
				else if (status == 403)
					_log.LogError("DeepL auth failed (HTTP 403) — API key invalid or inactive. target={Lang}", targetLang);
				else
					_log.LogWarning("DeepL HTTP {Status} for target={Lang}", status, targetLang);
				return null;
			}
			var body = await resp.Content.ReadAsStringAsync(ct);
			using var doc = JsonDocument.Parse(body);
			if (!doc.RootElement.TryGetProperty("translations", out var arr) || arr.GetArrayLength() == 0)
				return null;
			var first = arr[0];
			var translated = first.GetProperty("text").GetString() ?? "";
			var sourceLang = first.TryGetProperty("detected_source_language", out var d) ? d.GetString() : null;
			return new TranslationResult(translated, sourceLang ?? "");
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "DeepL translate failed");
			return null;
		}
	}
}
