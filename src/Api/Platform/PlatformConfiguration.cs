using Microsoft.AspNetCore.WebUtilities;
using Npgsql;

namespace Api.Platform;

/// <summary>
/// The one place that knows the platform's setting names (13-app-templates.md section 6.1 in
/// tl-cloud-infrastructure). It turns them into the options the framework's building blocks read, so nothing
/// else reads an environment variable. A setting the stamp does not set leaves its option as appsettings.json
/// has it, and the building block falls back to what needs no service. Add a mapping here for each setting
/// or secret the product adds. The migrator compiles this file through its reference to the API.
/// </summary>
public static class PlatformConfiguration
{
    /// <summary>The environment name when PLATFORM_ENVIRONMENT is unset.</summary>
    public const string LocalEnvironment = "local";

    /// <summary>
    /// Adds the options mapped from the platform's settings. Call it before anything reads the options;
    /// configuration added after it, such as a test host's, still wins. Throws when DATABASE_URL is unset:
    /// neither the API nor the migrator runs without Postgres.
    /// </summary>
    public static void AddPlatformSettings(this IConfigurationManager configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (Value(configuration, "DATABASE_URL") is not { } databaseUrl)
        {
            throw new InvalidOperationException(
                "DATABASE_URL is not set. The stamp sets it; locally, .env sets it for docker compose (docs/configuration.md).");
        }

        var settings = new Dictionary<string, string?>
        {
            ["Platform:Environment"] = Value(configuration, "PLATFORM_ENVIRONMENT") ?? LocalEnvironment,
            ["DatabaseOptions:Provider"] = "POSTGRESQL",
            ["DatabaseOptions:ConnectionString"] = ToNpgsqlConnectionString(databaseUrl),
            // The app's own context (Data/AppDbContext) reads the same connection string.
            ["ConnectionStrings:Database"] = ToNpgsqlConnectionString(databaseUrl),
        };

        Map(settings, configuration, "JWT_SIGNING_KEY", "JwtOptions:SigningKey");
        Map(settings, configuration, "HANGFIRE_PASSWORD", "HangfireOptions:Password");
        Map(settings, configuration, "SEED_ADMIN_PASSWORD", "Seed:DefaultAdminPassword");
        Map(settings, configuration, "SEED_TEST_DATA", "Seed:TestData");
        Map(settings, configuration, "REDIS_URL", "CachingOptions:Redis");
        // Mail goes through Azure Communication Services Email over SMTP (docs/configuration.md).
        Map(settings, configuration, "SMTP_HOST", "MailOptions:Smtp:Host");
        Map(settings, configuration, "SMTP_USERNAME", "MailOptions:Smtp:UserName");
        Map(settings, configuration, "SMTP_PASSWORD", "MailOptions:Smtp:Password");
        Map(settings, configuration, "MAIL_FROM", "MailOptions:From");

        configuration.AddInMemoryCollection(settings);
    }

    /// <summary>
    /// Turns a URL such as postgresql://user:password@host:5432/db?sslmode=require, the form the stamp's
    /// DATABASE_URL has, into an Npgsql connection string. A value that is already a connection string is
    /// returned as it is.
    /// </summary>
    internal static string ToNpgsqlConnectionString(string databaseUrl)
    {
        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("postgres" or "postgresql"))
        {
            return databaseUrl;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
        };

        var user = uri.UserInfo.Split(':', 2);
        if (user[0].Length > 0)
        {
            builder.Username = Uri.UnescapeDataString(user[0]);
        }
        if (user.Length == 2)
        {
            builder.Password = Uri.UnescapeDataString(user[1]);
        }

        foreach (var (key, value) in QueryHelpers.ParseQuery(uri.Query))
        {
            builder[key switch
            {
                "sslmode" => "SSL Mode",
                "channel_binding" => "Channel Binding",
                _ => key,
            }] = value.ToString();
        }

        return builder.ConnectionString;
    }

    private static void Map(Dictionary<string, string?> settings, IConfiguration configuration, string name, string key)
    {
        if (Value(configuration, name) is { } value)
        {
            settings[key] = value;
        }
    }

    private static string? Value(IConfiguration configuration, string name)
    {
        var value = configuration[name];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
