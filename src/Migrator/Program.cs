using System.Reflection;
using Api.Data;
using Api.Features.Staff; // feature:staff-area
using Api.Platform;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Migrator;
using Npgsql;
using Trislab.Framework.Eventing;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Tenancy;
using Trislab.Framework.Web;
using Trislab.Framework.Web.Modules;
using Trislab.Modules.Auditing; // module:auditing
using Trislab.Modules.Files; // module:files
using Trislab.Modules.Identity; // module:identity
using Trislab.Modules.Notifications; // module:notifications
using Trislab.Modules.Webhooks; // module:webhooks

// The init container of the api app: it runs to completion before the API starts, in every replica
// (13-app-templates.md section 5.3 in tl-cloud-infrastructure). It takes a Postgres advisory lock, applies the
// migrations of every module and of the app, seeds the first administrator, and with Seed:TestData also the
// test data, then exits 0, or 1 on a failure. The first replica migrates; the others wait for the lock, find
// nothing to do and exit. A failure keeps the revision from becoming ready, and the previous revision keeps
// serving. The framework's own migrator is not a package, so this one is the app's, on the same building
// blocks; it serves single-tenant mode only.

if (args.Any(a => a is "-h" or "--help"))
{
    Console.WriteLine("""
        The migrator of the app: applies the migrations of every module and of the app, and seeds the first
        administrator. It serves single-tenant mode only.

        Usage: dotnet run --project src/Migrator -- [options]

        Options:
          --test-data   Also seed the test data (the demo user and the sample items), as SEED_TEST_DATA=true does.
          -h, --help    Print this text.
        """);
    return 0;
}

var builder = Host.CreateApplicationBuilder(args);

// Validating the container at build walks every registration, including handlers this process never calls.
builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = false }));

// Under dotnet run the working folder is the project's, and appsettings.json is in the output folder.
builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true);
builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true);
// Environment variables and the command line keep priority over the files added above.
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);
builder.Configuration.AddPlatformSettings();

// Identity validates its signing key at start, and the migrator never signs a token: a labelled placeholder
// stands in when nothing real is set.
if (string.IsNullOrWhiteSpace(builder.Configuration["JwtOptions:SigningKey"]))
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["JwtOptions:SigningKey"] = "migrator-placeholder-never-signs-a-token-32+",
    });
}

// The mediator's handlers: Api/Program.cs lists the assemblies of the modules the product uses, and the
// mediator's source generator in the Api project registers them (a second generator here would clash). The
// same list of modules below is edited by `init-product` too (lines ending with `module:<name>`).
builder.Services.AddMediator(o => o.ServiceLifetime = ServiceLifetime.Scoped);

var moduleAssemblies = new Assembly[]
{
    typeof(IdentityModule).Assembly, // module:identity
    typeof(AuditingModule).Assembly, // module:auditing
    typeof(FilesModule).Assembly, // module:files
    typeof(NotificationsModule).Assembly, // module:notifications
    typeof(WebhooksModule).Assembly, // module:webhooks
};

StaffPermissions.Register(); // feature:staff-area
builder.AddTrislabTenancy(t => t.UseSingleTenant());
moduleAssemblies = ModuleLoader.SelectSupported(TenancyOptions.ReadMode(builder.Configuration), moduleAssemblies);

// Runtime concerns are off; persistence and tenancy stay on so the initializers resolve.
builder.AddTrislabPlatform(o =>
{
    o.EnableOpenTelemetry = false;
    o.EnableCors = false;
    o.EnableOpenApi = false;
    o.EnableJobs = false;
    o.EnableMailing = false;
    o.EnableSse = false;
    o.EnableRealtime = false;
    o.EnableQuotas = false;
    o.EnableFeatureFlags = false;
    o.EnableIdempotency = false;
    o.EnableCaching = true;
});
builder.Services.AddEventingCore(builder.Configuration);
builder.AddModules(moduleAssemblies);
builder.Services.AddAppDbContext(builder.Configuration);
builder.Services.AddScoped<TestDataSeeder>();

// Background services would poll tables before the migrations create them; only the migrator's steps run.
foreach (var descriptor in builder.Services
    .Where(d => d.ServiceType == typeof(IHostedService) && typeof(BackgroundService).IsAssignableFrom(d.ImplementationType))
    .ToList())
{
    builder.Services.Remove(descriptor);
}

using var host = builder.Build();
var log = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Migrator");

await host.StartAsync();
try
{
    var connectionString = builder.Configuration["DatabaseOptions:ConnectionString"]!;
    await WaitForDatabaseAsync(connectionString, log);

    // The lock is held on a connection of its own for the whole run, and released when it closes, also on a failure.
    await using var lockConnection = new NpgsqlConnection(connectionString);
    await lockConnection.OpenAsync();
    await using (var command = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", lockConnection))
    {
        command.Parameters.AddWithValue("key", 7_206_532_101L);
        log.LogInformation("Waiting for the migration lock");
        await command.ExecuteNonQueryAsync();
    }

    // Single-tenant mode has no tenant catalog: the one tenant comes from configuration.
    var tenants = await host.Services.GetRequiredService<IMultiTenantStore<AppTenantInfo, string>>().GetAllAsync(CancellationToken.None);
    var tenant = tenants.Single();

    log.LogInformation("Migrating the modules for the tenant {TenantId}", tenant.Id);
    await TenantInitializers.MigrateAsync(host.Services, tenant, CancellationToken.None);
    await TenantInitializers.SeedAsync(host.Services, tenant, CancellationToken.None);
    await StaffRoleSeeder.SeedRoleAsync(host.Services, tenant, CancellationToken.None); // feature:staff-area

    await using (var scope = host.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        log.LogInformation("Applying {Count} migrations of the app: {Migrations}", pending.Count, string.Join(", ", pending));
        await db.Database.MigrateAsync();
    }

    if (args.Contains("--test-data") || builder.Configuration.GetValue<bool>("Seed:TestData"))
    {
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TestDataSeeder>().RunAsync(tenant, CancellationToken.None);
    }

    log.LogInformation("The database is up to date");
    return 0;
}
catch (Exception e) when (e is NpgsqlException or InvalidOperationException or DbUpdateException or TimeoutException)
{
    log.LogCritical(e, "Migration failed");
    return 1;
}
finally
{
    await host.StopAsync();
}

// Postgres may still be starting; wait with a growing pause, for two minutes at most. A server that answers but has
// no database of that name yet is a shared Postgres (docs/local-ports.md): the migrator creates the database.
static async Task WaitForDatabaseAsync(string connectionString, ILogger log)
{
    var delay = TimeSpan.FromSeconds(1);
    var deadline = DateTime.UtcNow.AddMinutes(2);
    while (true)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            return;
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.InvalidCatalogName)
        {
            await CreateDatabaseAsync(connectionString, log);
            return;
        }
        catch (Exception e) when (e is NpgsqlException or System.Net.Sockets.SocketException)
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("Postgres did not answer within two minutes.", e);
            }

            log.LogWarning("Postgres is not ready, trying again in {Seconds} seconds", delay.TotalSeconds);
            await Task.Delay(delay);
            delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 1.5, 10));
        }
    }
}

// Creates the database of the connection string, through the server's maintenance database "postgres". Only when the
// database is missing, so a stamp, whose database the platform creates, never gets here. Two migrators that start
// at once may both try; the second one finds the database there.
static async Task CreateDatabaseAsync(string connectionString, ILogger log)
{
    var name = new NpgsqlConnectionStringBuilder(connectionString).Database
        ?? throw new InvalidOperationException("DATABASE_URL names no database.");
    var maintenance = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres", Pooling = false };
    await using var connection = new NpgsqlConnection(maintenance.ConnectionString);
    await connection.OpenAsync();

    // CREATE DATABASE takes no parameter; the name is quoted as an identifier, so it cannot end the statement.
    var quoted = "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
#pragma warning disable CA2100 // Justification: the only variable part is the database name, quoted as an identifier.
    await using var command = new NpgsqlCommand("CREATE DATABASE " + quoted, connection);
#pragma warning restore CA2100
    try
    {
        await command.ExecuteNonQueryAsync();
        log.LogInformation("Created the database {Database}", name);
    }
    catch (PostgresException e) when (e.SqlState is PostgresErrorCodes.DuplicateDatabase or PostgresErrorCodes.UniqueViolation)
    {
        log.LogInformation("The database {Database} was created by another migrator", name);
    }
}
