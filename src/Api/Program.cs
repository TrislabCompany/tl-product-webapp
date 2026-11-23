using System.Reflection;
using System.Text.Json.Serialization;
using Api.Data;
using Api.Features.Health;
using Api.Features.Items;
using Api.Platform;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Trislab.Framework.Eventing;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Tenancy;
using Trislab.Framework.Web;
using Trislab.Framework.Web.Modules;
using Trislab.Modules.Auditing; // module:auditing
using Trislab.Modules.Files; // module:files
using Trislab.Modules.Identity; // module:identity
using Trislab.Modules.Identity.Contracts.v1.Tokens.TokenGeneration; // module:identity
using Trislab.Modules.Identity.Features.v1.Tokens.TokenGeneration; // module:identity
using Trislab.Modules.Notifications; // module:notifications
using Trislab.Modules.Webhooks; // module:webhooks

var builder = WebApplication.CreateBuilder(args);

// The platform's settings, such as DATABASE_URL, become the options the framework reads, and only here.
builder.Configuration.AddPlatformSettings();

// Enums travel as string names, as the framework's own API sends them.
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

if (builder.Environment.IsProduction())
{
    // Postgres and the signing key of the sign-in tokens are required; everything else falls back to what
    // needs no service (an in-memory cache, mail only logged).
    foreach (var (key, setting) in new[] { ("DatabaseOptions:ConnectionString", "DATABASE_URL"), ("JwtOptions:SigningKey", "JWT_SIGNING_KEY") })
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration[key]))
        {
            throw new InvalidOperationException($"{setting} is not set. The stamp sets it; locally, .env sets it (docs/configuration.md).");
        }
    }
}

// The modules this product uses: the ones of tl-framework that support single-tenant mode (docs/modules.md).
// A line that ends with `module:<name>` belongs to that module: `init-product` removes the lines of the modules a
// product does not choose, here, in the migrator's Program.cs, in Api.csproj and in Directory.Packages.props
// (docs/modules.md). A module needs its Contracts type and its module type in the mediator's assemblies, and its
// assembly in moduleAssemblies; a missing mediator type leaves the module's handlers undiscovered.
builder.Services.AddMediator(o =>
{
    o.ServiceLifetime = ServiceLifetime.Scoped;
    o.Assemblies =
    [
        typeof(GenerateTokenCommand), // module:identity
        typeof(GenerateTokenCommandHandler), // module:identity
        typeof(Trislab.Modules.Auditing.Contracts.AuditEnvelope), // module:auditing
        typeof(Trislab.Modules.Auditing.Persistence.AuditDbContext), // module:auditing
        typeof(Trislab.Modules.Files.Contracts.v1.Commands.RequestUploadUrlCommand), // module:files
        typeof(FilesModule), // module:files
        typeof(Trislab.Modules.Notifications.Contracts.v1.Commands.MarkNotificationReadCommand), // module:notifications
        typeof(NotificationsModule), // module:notifications
        typeof(Trislab.Modules.Webhooks.Contracts.v1.CreateWebhookSubscription.CreateWebhookSubscriptionCommand), // module:webhooks
        typeof(WebhooksModule), // module:webhooks
    ];
});

var moduleAssemblies = new Assembly[]
{
    typeof(IdentityModule).Assembly, // module:identity
    typeof(AuditingModule).Assembly, // module:auditing
    typeof(FilesModule).Assembly, // module:files
    typeof(NotificationsModule).Assembly, // module:notifications
    typeof(WebhooksModule).Assembly, // module:webhooks
};

// Tenancy first: one fixed tenant, set in TenancyOptions:SingleTenant (docs/configuration.md). There is no
// tenant catalog, sign-up, plan or quota. SelectSupported drops any module the mode does not support.
builder.AddTrislabTenancy(t => t.UseSingleTenant());
moduleAssemblies = ModuleLoader.SelectSupported(TenancyOptions.ReadMode(builder.Configuration), moduleAssemblies);

builder.AddTrislabPlatform(o =>
{
    o.EnableCaching = true;
    o.EnableMailing = true;
    o.EnableJobs = true;
});

// The transactional outbox is framework infrastructure with one owner, so the host registers it once.
builder.Services.AddEventingCore(builder.Configuration);

builder.AddModules(moduleAssemblies);

// The app's own features: the sample's context, on the same Postgres.
builder.Services.AddAppDbContext(builder.Configuration);
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>(tags: ["ready"]);

var app = builder.Build();

app.UseTrislabTenancy();
app.UseTrislabPlatform(p =>
{
    // The front end's nginx serves the build and passes /api on; the API serves no files.
    p.ServeStaticFiles = false;
});

// Every route of the app is under /api, the path the front end's nginx passes on unchanged.
var api = app.MapGroup("/api");
api.MapHealthInfo();
api.MapItems();

// Liveness: the process answers. Readiness: every health check passes, Postgres included.
app.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/readyz").AllowAnonymous();

app.Logger.LogInformation("Starting in the environment {Environment}", app.Configuration["Platform:Environment"]);

await app.RunAsync();

/// <summary>The entry point, so the tests can start the API.</summary>
public partial class Program;
