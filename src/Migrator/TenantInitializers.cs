using Microsoft.Extensions.DependencyInjection;
using Trislab.Framework.Persistence;
using Trislab.Framework.Shared.Multitenancy;

namespace Migrator;

/// <summary>
/// Runs every registered module's <see cref="IDbInitializer"/> for the one fixed tenant. A fresh scope with the
/// tenant context set first, because a module's DbContext captures the tenant when it is built.
/// </summary>
internal static class TenantInitializers
{
    public static Task MigrateAsync(IServiceProvider services, AppTenantInfo tenant, CancellationToken cancellationToken) =>
        RunAsync(services, tenant, (init, ct) => init.MigrateAsync(ct), cancellationToken);

    public static Task SeedAsync(IServiceProvider services, AppTenantInfo tenant, CancellationToken cancellationToken) =>
        RunAsync(services, tenant, (init, ct) => init.SeedAsync(ct), cancellationToken);

    private static async Task RunAsync(
        IServiceProvider services,
        AppTenantInfo tenant,
        Func<IDbInitializer, CancellationToken, Task> step,
        CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();

        using var tenantUse = scope.ServiceProvider.GetRequiredService<ITenantAccessor>().Use(tenant);

        foreach (var initializer in scope.ServiceProvider.GetServices<IDbInitializer>())
        {
            await step(initializer, cancellationToken);
        }
    }
}
