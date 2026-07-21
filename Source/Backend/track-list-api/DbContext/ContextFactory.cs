using dotenv.net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace api.DbContext;

public static class ContextFactory
{
    private static DbContextOptions? _options;

    public static TrackListDbContext CreateNew()
    {
        var envOptions = new DotEnvOptions(ignoreExceptions: true);
        var env = DotEnv.Read(envOptions);

        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString) && !env.TryGetValue("CONNECTION_STRING", out connectionString))
        {
            // This connection string used for making migrations in docker
            connectionString = "Data Source=dummy.db";
            Console.WriteLine("⚠WARNING⚠: CONNECTION_STRING not found. Using dummy SQLite path.");
        }
        
        _options ??= new DbContextOptionsBuilder<TrackListDbContext>()
            .UseLazyLoadingProxies()
            .EnableSensitiveDataLogging()
            .LogTo(Console.WriteLine)
            .UseSqlite(connectionString)
            .Options;

        return new TrackListDbContext(_options);
    }
}

public class DbContextDesignTimeFactory : IDesignTimeDbContextFactory<TrackListDbContext>
{
    public TrackListDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<TrackListDbContext>();
        
        // This connection string used for making migrations in docker
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Data Source=dummy.db";
            
            try
            {
                // Ignoring exceptions for proper use of dummy connection string
                var envOptions = new DotEnvOptions(ignoreExceptions: true);
                var env = DotEnv.Read(envOptions);

                // Якщо файл прочитано і змінна існує (наприклад, локально), використовуємо її
                if (env.TryGetValue("CONNECTION_STRING", out var realConnectionString))
                {
                    connectionString = realConnectionString;
                }
            }
            catch
            {
                //Just ignore exceptions on docker migrations.
            }
        }

        builder.UseSqlite(connectionString);

        return new TrackListDbContext(builder.Options);
    }
}
