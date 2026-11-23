using Api.Features.Staff;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trislab.Framework.Shared.Constants;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Identity.Data;
using Trislab.Modules.Identity.Domain;

namespace Migrator;

/// <summary>
/// Creates the custom role `Staff`, which holds only the permission that opens the staff area, for people who
/// must see the staff pages and nothing else (docs/staff-area.md). Administrators have the permission through
/// the built-in Admin role. Idempotent: it runs on every migration. The demo staff user is test data.
/// </summary>
internal static class StaffRoleSeeder
{
    public const string RoleName = "Staff";

    public const string DemoStaffEmail = "staff@example.com";

    public static async Task SeedRoleAsync(IServiceProvider services, AppTenantInfo tenant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<TrislabRole>>();
        var role = await roles.FindByNameAsync(RoleName);
        if (role is null)
        {
            role = new TrislabRole(RoleName, "Staff: can use the staff area");
            var created = await roles.CreateAsync(role);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException($"The role {RoleName} was not created: {string.Join("; ", created.Errors.Select(e => e.Code))}");
            }
        }

        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var claims = await roles.GetClaimsAsync(role);
        if (!claims.Any(c => c.Type == ClaimConstants.Permission && c.Value == StaffPermissions.Access))
        {
            db.RoleClaims.Add(new TrislabRoleClaim
            {
                RoleId = role.Id,
                ClaimType = ClaimConstants.Permission,
                ClaimValue = StaffPermissions.Access,
                CreatedBy = nameof(StaffRoleSeeder),
                CreatedOn = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Test data: a demo user in the Staff role, with the public demo password.</summary>
    public static async Task SeedDemoStaffAsync(IServiceProvider provider, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(logger);

        var users = provider.GetRequiredService<UserManager<TrislabUser>>();
        if (await users.FindByEmailAsync(DemoStaffEmail) is not null)
        {
            return;
        }

        var user = new TrislabUser
        {
            UserName = "demo.staff",
            Email = DemoStaffEmail,
            EmailConfirmed = true,
            FirstName = "Demo",
            LastName = "Staff",
            IsActive = true,
        };
        var created = await users.CreateAsync(user, TestDataSeeder.DemoPassword);
        if (!created.Succeeded)
        {
            logger.LogWarning("The demo staff user was not created: {Errors}", string.Join("; ", created.Errors.Select(e => e.Code)));
            return;
        }

        await users.AddToRoleAsync(user, RoleName);
        logger.LogInformation("Created the demo staff user");
    }
}
