using Api.Data;
using Api.Features.Items;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Identity.Data;
using Trislab.Modules.Identity.Domain;

namespace Migrator;

/// <summary>
/// Test data for the sandbox and for local runs: a demo user and demo items, with a public password. The
/// framework's <c>seed-demo</c> is refused in single-tenant mode because it creates demo tenants, so this is
/// the app's own seeder. It runs only where <c>Seed:TestData</c> is true (SEED_TEST_DATA), never in an
/// Environment with real data, and it is idempotent. Add the demo data of each feature of the product here.
/// </summary>
internal sealed class TestDataSeeder(IServiceProvider services, ILogger<TestDataSeeder> logger)
{
    /// <summary>The password of every demo user. It is public: never use it where real people sign in.</summary>
    public const string DemoPassword = "Password123!";

    public const string DemoUserEmail = "user@example.com";

    public async Task RunAsync(AppTenantInfo tenant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        using var scope = services.CreateScope();
        using var tenantUse = scope.ServiceProvider.GetRequiredService<ITenantAccessor>().Use(tenant);

        await SeedDemoUserAsync(scope.ServiceProvider);
        await StaffRoleSeeder.SeedDemoStaffAsync(scope.ServiceProvider, logger); // feature:staff-area
        await SeedItemsAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), cancellationToken);
        await InvoiceDocumentsDemoData.SeedAsync(scope.ServiceProvider.GetRequiredService<Mediator.IMediator>(), logger, cancellationToken); // module:invoice-documents
    }

    private async Task SeedDemoUserAsync(IServiceProvider provider)
    {
        var users = provider.GetRequiredService<UserManager<TrislabUser>>();
        if (await users.FindByEmailAsync(DemoUserEmail) is not null)
        {
            return;
        }

        var user = new TrislabUser
        {
            UserName = "demo.user",
            Email = DemoUserEmail,
            EmailConfirmed = true,
            FirstName = "Demo",
            LastName = "User",
        };
        var created = await users.CreateAsync(user, DemoPassword);
        if (!created.Succeeded)
        {
            logger.LogWarning("The demo user was not created: {Errors}", string.Join("; ", created.Errors.Select(e => e.Code)));
            return;
        }

        await AddMembershipAsync(provider, user);

        await users.AddToRoleAsync(user, "Basic");
        logger.LogInformation("Created the demo user");
    }

    /// <summary>
    /// Users are global accounts; a membership gives one access to this tenant. A demo user is a member from the
    /// start, as if it had accepted an invitation. Call it in the tenant's scope, before assigning roles.
    /// </summary>
    internal static async Task AddMembershipAsync(IServiceProvider provider, TrislabUser user)
    {
        var tenant = provider.GetRequiredService<ITenantAccessor>().TenantInfo
            ?? throw new InvalidOperationException("A demo user is seeded in the tenant's scope.");
        var db = provider.GetRequiredService<IdentityDbContext>();
        db.TenantMemberships.Add(TenantMembership.CreateAccepted(user.Id, tenant.Id));
        await db.SaveChangesAsync();
    }

    private async Task SeedItemsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Items.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Items.AddRange(
            new Item { Name = "Read the README" },
            new Item { Name = "Run the app with docker compose" },
            new Item { Name = "Replace this sample with the product's first feature" });
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created the demo items");
    }
}
