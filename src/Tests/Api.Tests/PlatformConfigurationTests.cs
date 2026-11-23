using Api.Platform;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Api.Tests;

public class PlatformConfigurationTests
{
    [Fact]
    public void Maps_a_database_url_to_a_connection_string()
    {
        var connectionString = PlatformConfiguration.ToNpgsqlConnectionString(
            "postgresql://app%40x:p%3Ass@db.example.com:6543/my-db?sslmode=require&channel_binding=require");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Equal("db.example.com", builder.Host);
        Assert.Equal(6543, builder.Port);
        Assert.Equal("my-db", builder.Database);
        Assert.Equal("app@x", builder.Username);
        Assert.Equal("p:ss", builder.Password);
        Assert.Equal(SslMode.Require, builder.SslMode);
        Assert.Equal(ChannelBinding.Require, builder.ChannelBinding);
    }

    [Fact]
    public void Uses_port_5432_when_the_url_has_none()
    {
        var builder = new NpgsqlConnectionStringBuilder(PlatformConfiguration.ToNpgsqlConnectionString("postgres://app:app@postgres/app"));

        Assert.Equal(5432, builder.Port);
    }

    [Fact]
    public void Keeps_a_connection_string_as_it_is()
    {
        const string connectionString = "Host=localhost;Database=app;Username=app;Password=app";

        Assert.Equal(connectionString, PlatformConfiguration.ToNpgsqlConnectionString(connectionString));
    }

    [Fact]
    public void Maps_the_platform_settings()
    {
        var configuration = Configure(new() { ["DATABASE_URL"] = "postgres://app:app@postgres/app", ["PLATFORM_ENVIRONMENT"] = "dev" });

        Assert.Equal("dev", configuration["Platform:Environment"]);
        Assert.Equal("postgres", new NpgsqlConnectionStringBuilder(configuration["DatabaseOptions:ConnectionString"]).Host);
    }

    [Fact]
    public void Names_the_environment_local_when_PLATFORM_ENVIRONMENT_is_unset()
    {
        var configuration = Configure(new() { ["DATABASE_URL"] = "postgres://app:app@postgres/app" });

        Assert.Equal(PlatformConfiguration.LocalEnvironment, configuration["Platform:Environment"]);
    }

    [Fact]
    public void Gives_the_app_context_the_same_connection_string_as_the_framework()
    {
        var configuration = Configure(new() { ["DATABASE_URL"] = "postgres://app:app@postgres/app" });

        Assert.Equal(configuration["DatabaseOptions:ConnectionString"], configuration.GetConnectionString("Database"));
        Assert.Equal("POSTGRESQL", configuration["DatabaseOptions:Provider"]);
    }

    [Fact]
    public void Maps_the_secrets_and_the_mail_settings_to_the_framework_options()
    {
        var configuration = Configure(new()
        {
            ["DATABASE_URL"] = "postgres://app:app@postgres/app",
            ["JWT_SIGNING_KEY"] = "key",
            ["SEED_ADMIN_PASSWORD"] = "admin-password",
            ["SEED_TEST_DATA"] = "true",
            ["SMTP_USERNAME"] = "smtp-user",
            ["SMTP_PASSWORD"] = "smtp-password",
            ["MAIL_FROM"] = "DoNotReply@example.com",
        });

        Assert.Equal("key", configuration["JwtOptions:SigningKey"]);
        Assert.Equal("admin-password", configuration["Seed:DefaultAdminPassword"]);
        Assert.Equal("true", configuration["Seed:TestData"]);
        Assert.Equal("smtp-user", configuration["MailOptions:Smtp:UserName"]);
        Assert.Equal("smtp-password", configuration["MailOptions:Smtp:Password"]);
        Assert.Equal("DoNotReply@example.com", configuration["MailOptions:From"]);
    }

    [Fact]
    public void Leaves_an_unset_setting_as_appsettings_has_it()
    {
        var configuration = Configure(new() { ["DATABASE_URL"] = "postgres://app:app@postgres/app", ["JWT_SIGNING_KEY"] = " " });

        Assert.Null(configuration["JwtOptions:SigningKey"]);
    }

    [Fact]
    public void Throws_when_DATABASE_URL_is_unset()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Configure([]));

        Assert.Contains("DATABASE_URL", exception.Message, StringComparison.Ordinal);
    }

    private static ConfigurationManager Configure(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(settings);
        configuration.AddPlatformSettings();
        return configuration;
    }
}
