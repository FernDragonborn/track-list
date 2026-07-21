using System.Globalization;
using System.IO.Compression;
using System.Threading.RateLimiting;
using api.DbContext;
using api.Middleware;
using api.Repository;
using api.Services;
using api.Services.IServices;
using api.Utils;
using dotenv.net;
using FluentValidation;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Logging;
using Serilog;
using Serilog.Events;

namespace api;

public static class Program
{
	public static void Main(string[] args)
	{
		CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
		CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

		Configure.AddUkrainianLanguageSupport();

		DotEnv.Load(new DotEnvOptions(
			ignoreExceptions: false,
			trimValues: true,
			overwriteExistingVars: false));

		Configure.CreateRootDirectoryIfNotExists();

		// Serilog: structured logging to console + file
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Information()
			.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
			.MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
			.Enrich.FromLogContext()
			.WriteTo.Console(outputTemplate:
				"[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
			.WriteTo.File("logs/api-.log",
				rollingInterval: RollingInterval.Day,
				retainedFileCountLimit: 30,
				outputTemplate:
				"{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
			.CreateLogger();

		var builder = WebApplication.CreateBuilder(args);

		builder.Host.UseSerilog();

		builder.Configuration.AddJsonFile("appsettings.json", true, true);
		if (SelfHostSecurityOptions.ProductionSecretsLookUnsafe(builder.Environment))
			throw new InvalidOperationException("JWT_PRIVATE_KEY must be set to a fresh 32+ character secret in production.");

		builder.Services.AddValidatorsFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());

		Configure.AddIfDevelopmentSuppressModelStateInvalidFilter(builder);

		Configure.ConfigControllers<TrackListDbContext>(builder, "CONNECTION_STRING");
		Configure.ConfigureNewtonJson();



		builder.Services.AddCors(options =>
		{
			var allowedOrigins = SelfHostSecurityOptions.AllowedOrigins(builder.Environment);
			options.AddPolicy("DefaultCors", policy =>
			{
				policy.AllowAnyHeader()
					.AllowAnyMethod();

				if (allowedOrigins.Length > 0)
					policy.WithOrigins(allowedOrigins).AllowCredentials();
			});
		});

		builder.Services.AddRateLimiter(options =>
		{
			options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
			options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
				RateLimitPartition.GetFixedWindowLimiter(
					GetRateLimitPartitionKey(context),
					_ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = 300,
						Window = TimeSpan.FromMinutes(1),
						QueueLimit = 0,
						AutoReplenishment = true
					}));
			options.AddPolicy("auth", context =>
				RateLimitPartition.GetFixedWindowLimiter(
					$"auth:{GetRateLimitPartitionKey(context)}",
					_ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = 10,
						Window = TimeSpan.FromMinutes(1),
						QueueLimit = 0,
						AutoReplenishment = true
					}));
			options.AddPolicy("write", context =>
				RateLimitPartition.GetFixedWindowLimiter(
					$"write:{GetRateLimitPartitionKey(context)}",
					_ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = 60,
						Window = TimeSpan.FromMinutes(1),
						QueueLimit = 0,
						AutoReplenishment = true
					}));
			options.AddPolicy("expensive", context =>
				RateLimitPartition.GetFixedWindowLimiter(
					$"expensive:{GetRateLimitPartitionKey(context)}",
					_ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = 20,
						Window = TimeSpan.FromMinutes(1),
						QueueLimit = 0,
						AutoReplenishment = true
					}));
		});

		Configure.AddCompression(builder, CompressionLevel.Optimal);

		Configure.AddSwagger(builder);

		Configure.AddAuthenticationAndAuthorisation(builder);
		
		builder.Services.Configure<FormOptions>(options => { options.MultipartBodyLengthLimit = 10000000; });
		builder.Services.AddHttpClient();
		builder.Services.AddScoped<IMediaGetService, MediaGetService>();
		builder.Services.AddScoped<IReportService, ReportService>();
		builder.Services.AddScoped<IMediaOperationService, MediaOperationService>();
		builder.Services.AddScoped<IMediaExternalService, TmdbService>();
		builder.Services.AddScoped<IAuthService, AuthService>();
		builder.Services.AddScoped<IUserService, UserService>();
		builder.Services.AddScoped<IReviewService, ReviewService>();
		builder.Services.AddScoped<ICollectionService, CollectionService>();
		builder.Services.AddScoped<IFeedService, FeedService>();
		builder.Services.AddScoped<IExternalContentService, ExternalContentService>();
		builder.Services.AddScoped<IExternalReviewerService, ExternalReviewerService>();
		builder.Services.AddScoped<ITranslationService, TranslationService>();
		builder.Services.AddScoped<IPublicStatsService, PublicStatsService>();
		builder.Services.AddSingleton<api.Services.External.OmdbClient>();
		builder.Services.AddSingleton<api.Services.External.WikipediaClient>();
		builder.Services.AddSingleton<api.Services.External.LetterboxdRssClient>();
		builder.Services.AddSingleton<api.Services.External.DeeplClient>();
		if (SelfHostSecurityOptions.AnyExternalContentEnabled(builder.Environment))
			builder.Services.AddHostedService<ExternalContentRefreshService>();

		builder.Services.AddAutoMapper(cfg =>
		{
			/* configuration */
		}, AppDomain.CurrentDomain.GetAssemblies());

		builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

		var app = builder.Build();

		Configure.MigrateDbAndSeedReferenceData(app);

		app.UseMiddleware<GlobalExceptionHandler>();
		app.UseSerilogRequestLogging();

		Configure.IfIsDevelopmentUseSwaggerElseHsts(app);

		IdentityModelEventSource.ShowPII = app.Environment.IsDevelopment()
		                                   && SelfHostSecurityOptions.GetBool("TRACKLIST_SHOW_PII", false);

		app.UseHttpsRedirection();

		app.UseRouting();
		app.UseCors("DefaultCors");
		app.UseRateLimiter();

		app.UseAuthentication();
		app.UseAuthorization();

		app.MapControllers();

		app.Run();
	}

	private static string GetRateLimitPartitionKey(HttpContext context) =>
		context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
		?? context.Connection.RemoteIpAddress?.ToString()
		?? "unknown";
}
