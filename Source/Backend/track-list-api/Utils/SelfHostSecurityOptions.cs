using dotenv.net;

namespace api.Utils;

internal static class SelfHostSecurityOptions
{
	private static readonly IDictionary<string, string> Env = DotEnv.Read();

	internal static string? Get(string key)
	{
		var value = Environment.GetEnvironmentVariable(key)
		            ?? (Env.TryGetValue(key, out var fromFile) ? fromFile : null);
		return string.IsNullOrWhiteSpace(value) ? null : value.Trim().Trim('"');
	}

	internal static bool IsDevelopmentLike(IHostEnvironment? env = null)
	{
		var environment = env?.EnvironmentName
		                  ?? Get("ASPNETCORE_ENVIRONMENT")
		                  ?? Get("DOTNET_ENVIRONMENT")
		                  ?? "Production";

		return environment.Equals("Development", StringComparison.OrdinalIgnoreCase)
		       || environment.Equals("Test", StringComparison.OrdinalIgnoreCase);
	}

	internal static bool GetBool(string key, bool defaultValue)
	{
		var raw = Get(key);
		if (raw is null) return defaultValue;

		return raw.Equals("1", StringComparison.OrdinalIgnoreCase)
		       || raw.Equals("true", StringComparison.OrdinalIgnoreCase)
		       || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
		       || raw.Equals("on", StringComparison.OrdinalIgnoreCase);
	}

	internal static bool PublicRegistrationEnabled(IHostEnvironment? env = null) =>
		GetBool("TRACKLIST_PUBLIC_REGISTRATION", IsDevelopmentLike(env));

	internal static int? MaxUsers(IHostEnvironment? env = null)
	{
		var raw = Get("TRACKLIST_MAX_USERS");
		if (raw is null) return IsDevelopmentLike(env) ? null : 1;
		if (raw.Equals("unlimited", StringComparison.OrdinalIgnoreCase)) return null;
		return int.TryParse(raw, out var parsed) && parsed >= 0 ? parsed : 1;
	}

	internal static bool ExternalServiceEnabled(string key, IHostEnvironment? env = null) =>
		GetBool(key, IsDevelopmentLike(env));

	internal static bool AnyExternalContentEnabled(IHostEnvironment? env = null) =>
		ExternalServiceEnabled("TRACKLIST_ENABLE_TMDB", env)
		|| ExternalServiceEnabled("TRACKLIST_ENABLE_OMDB", env)
		|| ExternalServiceEnabled("TRACKLIST_ENABLE_DEEPL", env)
		|| ExternalServiceEnabled("TRACKLIST_ENABLE_LETTERBOXD", env)
		|| ExternalServiceEnabled("TRACKLIST_ENABLE_WIKIPEDIA", env);

	internal static string[] AllowedOrigins(IHostEnvironment env)
	{
		var configured = Get("TRACKLIST_ALLOWED_ORIGINS");
		if (!string.IsNullOrWhiteSpace(configured))
			return configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Where(origin => !origin.Equals("*", StringComparison.Ordinal))
				.ToArray();

		return IsDevelopmentLike(env)
			?
			[
				"http://localhost",
				"http://localhost:5173",
				"http://127.0.0.1",
				"http://127.0.0.1:5173"
			]
			: [];
	}

	internal static bool ProductionSetupTokenRequired(IHostEnvironment env) => !IsDevelopmentLike(env);

	internal static bool ProductionSecretsLookUnsafe(IHostEnvironment env)
	{
		if (IsDevelopmentLike(env)) return false;

		var jwtKey = Get("JWT_PRIVATE_KEY");
		return string.IsNullOrWhiteSpace(jwtKey)
		       || jwtKey.Length < 32
		       || jwtKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
		       || jwtKey.Contains("please_change_me", StringComparison.OrdinalIgnoreCase);
	}
}
