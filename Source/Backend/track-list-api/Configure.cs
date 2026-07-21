using api.Services;
using api.Utils;
using dotenv.net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Swashbuckle.AspNetCore.Filters;
using System.IO.Compression;
using System.Text;
using api.DbContext;
using api.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace api;

internal static class Configure
{
    private static readonly IDictionary<string, string> Env = DotEnv.Read();

    internal static void AddUkrainianLanguageSupport()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Console.OutputEncoding = Encoding.GetEncoding(1251);
        Console.InputEncoding = Encoding.GetEncoding(1251);
    }

    internal static void CreateRootDirectoryIfNotExists()
    {
        var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), StaticDetails.UserProfileImagePath);
        if (!Directory.Exists(uploadsPath))
        {
            Directory.CreateDirectory(uploadsPath);
            Console.WriteLine($"Created directory: {uploadsPath}");
        }
    }

    internal static void AddLogs(WebApplicationBuilder builder)
    {
        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Information);
            builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Debug);
            builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
            builder.Logging.AddFilter("Default", LogLevel.Warning);
        });
        builder.Services.AddHttpLogging(logging =>
        {
            logging.LoggingFields = HttpLoggingFields.Request | HttpLoggingFields.ResponseHeaders;
            logging.MediaTypeOptions.AddText("application/javascript");
            logging.RequestBodyLogLimit = 4096;
            logging.ResponseBodyLogLimit = 4096;
            logging.CombineLogs = true;
        });
    }

    internal static void ConfigureNewtonJson()
    {
        JsonConvert.DefaultSettings = () =>
        {
            JsonSerializerSettings settings = new()
            {
                MaxDepth = 16
            };
            return settings;
        };
    }

    internal static void ConfigControllers<TContext>(WebApplicationBuilder builder, string connectionStringName)
        where TContext : Microsoft.EntityFrameworkCore.DbContext
    {
        // Add services to the container.
        builder.Services.AddControllers()
            .AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy
                    {
                        ProcessDictionaryKeys = true,
                        OverrideSpecifiedNames = false
                    }
                };


                options.SerializerSettings.Formatting = Newtonsoft.Json.Formatting.Indented;
                options.SerializerSettings.DateFormatString = "yyyy-MM-dd";
                options.SerializerSettings.NullValueHandling = NullValueHandling.Include;
                options.SerializerSettings.MissingMemberHandling = MissingMemberHandling.Ignore;
                options.SerializerSettings.TypeNameHandling = TypeNameHandling.None;
                options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                options.SerializerSettings.MetadataPropertyHandling = MetadataPropertyHandling.Ignore;

                options.SerializerSettings.Converters.Add(new StringEnumConverter(new CamelCaseNamingStrategy()));
                options.SerializerSettings.Converters.Add(new DateOnlyConverter());
            });

            var connectionString =
				builder.Configuration.GetConnectionString(connectionStringName)
				?? builder.Configuration[connectionStringName]
				?? (Env.TryGetValue(connectionStringName, out var v) ? v : null);

			if (string.IsNullOrWhiteSpace(connectionString))
			{
				throw new InvalidOperationException(
					$"Missing required configuration key '{connectionStringName}'. " +
					"Set it as an environment variable or in appsettings.");
			}

			builder.Services.AddDbContext<TContext>(opts => opts.UseSqlite(connectionString));
    }

    internal static void AddIfDevelopmentSuppressModelStateInvalidFilter(WebApplicationBuilder builder)
    {
        //Uncomment for api models problems
        //https://mirsaeedi.medium.com/asp-net-core-customize-validation-error-message-9022c12d3d7d
        if (builder.Environment.IsDevelopment())
            builder.Services.Configure<ApiBehaviorOptions>(apiBehaviorOptions =>
            {
                apiBehaviorOptions.SuppressModelStateInvalidFilter = true;
            });
    }

    internal static void AddCompression(WebApplicationBuilder builder, CompressionLevel compressionLevel)
    {
        builder.Services.AddResponseCompression(options =>
        {
            options.Providers.Add<GzipCompressionProvider>();
            options.EnableForHttps = true;
        });

        builder.Services.Configure<GzipCompressionProviderOptions>(options => { options.Level = compressionLevel; });
    }

    internal static void AddSwagger(WebApplicationBuilder builder)
    {
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer abcdef12345\"",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT"
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("bearer", document)] = []
            });
            options.OperationFilter<SecurityRequirementsOperationFilter>();
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1",
                Title = " API",
                Description = "An ASP.NET Core Web API for UActive wep-app"
            });
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

        });
    }

    internal static void AddAuthenticationAndAuthorisation(WebApplicationBuilder builder)
    {
        builder.Services.AddAuthentication(x =>
        {
            x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            x.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(x =>
        {
            x.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKey = JwtHandler.GetPrivateKey(),
                ValidIssuer = JwtHandler.GetIssuer(),
                ValidAudience = JwtHandler.GetAudience(),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.Zero
            };
            x.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            x.SaveToken = true;
        });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(IdentityData.PolicyAdmin, p =>
                p.RequireRole(IdentityData.ClaimAdmin.ToString(), IdentityData.ClaimAdmin.ToString()));
            options.AddPolicy(IdentityData.PolicyModerator, p =>
                p.RequireRole(IdentityData.ClaimModerator.ToString(), IdentityData.ClaimAdmin.ToString()));
            options.AddPolicy(IdentityData.PolicyUser, p =>
                p.RequireRole(IdentityData.ClaimUser.ToString(), IdentityData.ClaimModerator.ToString(), IdentityData.ClaimAdmin.ToString()));
        });
    }

    internal static void IfIsDevelopmentUseSwaggerElseHsts(WebApplication app)
    {
        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
                options.RoutePrefix = string.Empty;
            });
        }
        else
        {
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }
    }

    internal static void MigrateDbAndSeedReferenceData(WebApplication app)
    {
	    using var scope = app.Services.CreateScope();
	    var logger = scope.ServiceProvider.GetRequiredService<ILogger<TrackListDbContext>>();
	    var db = scope.ServiceProvider.GetRequiredService<TrackListDbContext>();

	    logger.LogInformation("Database check: connecting to {Provider}...", db.Database.ProviderName);
	    var sqliteDatabasePath = GetSqliteDatabasePath(db);
	    var sqliteDatabaseExisted = sqliteDatabasePath is not null && File.Exists(sqliteDatabasePath);
	    var pendingMigrations = db.Database.GetPendingMigrations().ToArray();

	    if (pendingMigrations.Length == 0)
	    {
		    logger.LogInformation("Database schema is up to date.");
	    }
	    else
	    {
		    logger.LogInformation(
			    "Database pending migrations: {PendingMigrations}",
			    string.Join(", ", pendingMigrations));
		    BackupSqliteDatabaseBeforeMigration(db, sqliteDatabasePath, sqliteDatabaseExisted, pendingMigrations, logger);
	    }

	    db.Database.Migrate();
	    logger.LogInformation("Database migrations applied.");

	    ApplySqlitePragmas(db, logger);
	    GenreSeeder.Seed(db, logger);
    }

    private static void BackupSqliteDatabaseBeforeMigration(
	    TrackListDbContext db,
	    string? sqliteDatabasePath,
	    bool sqliteDatabaseExisted,
	    string[] pendingMigrations,
	    ILogger logger)
    {
	    if (!IsSqlite(db))
		    return;

	    if (sqliteDatabasePath is null)
	    {
		    logger.LogInformation(
			    "SQLite database is in-memory or URI-based; backup skipped before migrations: {PendingMigrations}",
			    string.Join(", ", pendingMigrations));
		    return;
	    }

	    if (!sqliteDatabaseExisted || new FileInfo(sqliteDatabasePath).Length == 0)
	    {
		    logger.LogInformation("No existing SQLite database found; migration will create a new database.");
		    return;
	    }

	    var dbDirectory = Path.GetDirectoryName(sqliteDatabasePath)
	                      ?? throw new InvalidOperationException("SQLite database path must include a directory.");
	    var backupDirectory = Path.Combine(dbDirectory, "backups");
	    Directory.CreateDirectory(backupDirectory);

	    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
	    var databaseName = Path.GetFileNameWithoutExtension(sqliteDatabasePath);
	    var backupPath = Path.Combine(backupDirectory, $"{databaseName}-pre-migrate-{timestamp}.db");

	    try
	    {
		    var sourceBuilder = new SqliteConnectionStringBuilder(db.Database.GetConnectionString())
		    {
			    DataSource = sqliteDatabasePath
		    };
		    using var source = new SqliteConnection(sourceBuilder.ConnectionString);
		    using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
		    {
			    DataSource = backupPath
		    }.ConnectionString);

		    source.Open();
		    destination.Open();
		    source.BackupDatabase(destination);

		    logger.LogInformation(
			    "SQLite database backup created before migrations. Source: {Source}; Backup: {Backup}; Pending migrations: {PendingMigrations}",
			    sqliteDatabasePath,
			    backupPath,
			    string.Join(", ", pendingMigrations));
	    }
	    catch (Exception ex)
	    {
		    throw new InvalidOperationException("Failed to create SQLite database backup before migrations.", ex);
	    }
    }

    private static string? GetSqliteDatabasePath(TrackListDbContext db)
    {
	    if (!IsSqlite(db))
		    return null;

	    var connectionString = db.Database.GetConnectionString();
	    if (string.IsNullOrWhiteSpace(connectionString))
		    return null;

	    var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
	    if (string.IsNullOrWhiteSpace(dataSource)
	        || dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
	        || dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
		    return null;

	    return Path.GetFullPath(
		    dataSource,
		    Path.IsPathFullyQualified(dataSource) ? Path.GetPathRoot(dataSource)! : Directory.GetCurrentDirectory());
    }

    private static void ApplySqlitePragmas(TrackListDbContext db, ILogger logger)
    {
	    // SQLite tuning: WAL for concurrent reads + enforce FK constraints (off by default).
	    if (IsSqlite(db))
	    {
		    try
		    {
			    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
			    db.Database.ExecuteSqlRaw("PRAGMA foreign_keys=ON;");
			    logger.LogInformation("SQLite pragmas applied: journal_mode=WAL, foreign_keys=ON");
		    }
		    catch (Exception ex)
		    {
			    logger.LogWarning(ex, "Failed to apply SQLite pragmas");
		    }
	    }
    }

    private static bool IsSqlite(TrackListDbContext db) =>
	    db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
}
