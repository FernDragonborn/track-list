using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace TrackListTests;

public static class TestConstants
{
	public const string JwtSecretKey = "super_long_secret_key_please_change_me";
	public const string JwtIssuer = "track-list-api";
	public const string JwtAudience = "track-list-web";
	public const string DefaultUserId = "00000000-0000-0000-0000-000000000001";
	public const string DefaultTestEmail = "test@example.com";
	public const string TempPassword = "TempPassword123!";

	public const string ValidJwtStructure =
		"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.XbPfbIHMI6arZ3Y922BhjWgQzWXcXNrz0ogrJ63nMj0";
}

public class Helpers
{
	public static void ThenResponseCodeIs(int expectedCode, IActionResult? lastResult)
	{
		Assert.NotNull(lastResult);

		var statusCode = lastResult switch
		{
			OkResult => 200,
			OkObjectResult => 200,
			NoContentResult => 204,
			BadRequestResult => 400,
			BadRequestObjectResult => 400,
			UnauthorizedResult => 401,
			UnauthorizedObjectResult => 401,
			FileContentResult => 200,
			FileStreamResult => 200,
			ForbidResult => 403,
			NotFoundResult => 404,
			NotFoundObjectResult => 404,
			_ => (int?)lastResult.GetType().GetProperty("StatusCode")?.GetValue(lastResult) ?? 0,
		};

		Assert.Equal(expectedCode, statusCode);
	}
}