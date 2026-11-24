using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Integration.Tests.ApiFactory))]

namespace Integration.Tests;

/// <summary>
/// The API against a throw-away Postgres 17 container that the tests start themselves, migrated by the real
/// migrator with the test data, in single-tenant mode as appsettings.json sets it. One for all tests.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>The first administrator's password, which the migrator seeds from SEED_ADMIN_PASSWORD.</summary>
    public const string AdminPassword = "Admin-Test-Pa55word";

    /// <summary>The first administrator of appsettings.json's single tenant.</summary>
    public const string AdminEmail = "admin@example.com";

    // The Postgres of docker-compose.yml and of a dev stamp.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(
        "postgres:17.11-alpine@sha256:266d3c257594e195b309ae9d1805e54c788f1d7b375f56bf6c367aac6c917e7b").Build();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // The API and the migrator read the platform's settings from the environment, in the form the stamp sets.
        // The database does not exist yet, as on a shared Postgres (docs/local-ports.md): the migrator creates it.
        var b = new Npgsql.NpgsqlConnectionStringBuilder(_postgres.GetConnectionString());
        var settings = new Dictionary<string, string>
        {
            ["DATABASE_URL"] = $"postgresql://{b.Username}:{b.Password}@{b.Host}:{b.Port}/trislab_webapp_test",
            ["PLATFORM_ENVIRONMENT"] = "test",
            ["JWT_SIGNING_KEY"] = "integration-test-signing-key-integration-test-signing-key",
            ["SEED_ADMIN_PASSWORD"] = AdminPassword,
            ["SEED_TEST_DATA"] = "true",
            ["StaffArea__Enabled"] = "true",
        };
        foreach (var (name, value) in settings)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        await RunMigratorAsync();

        // Start the host once, here: the framework registers its permissions in a static list that is not safe
        // for test classes that would otherwise start the host at the same time.
        _ = Services;
    }

    // As in an Environment: Development would validate the whole container at start, and fail on the framework's
    // handlers that need services this API does not register (the framework's own migrator turns that check off too).
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment("Production");

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // The migrator is a project reference, so its build sits next to the tests; it runs as it does in its image.
    private static async Task RunMigratorAsync()
    {
        var start = new ProcessStartInfo("dotnet", $"\"{Path.Combine(AppContext.BaseDirectory, "Migrator.dll")}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"The migrator failed ({process.ExitCode}): {await output}{await error}");
        }
    }
}
