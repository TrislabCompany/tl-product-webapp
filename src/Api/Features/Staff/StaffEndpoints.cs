using Api.Data;
using Microsoft.EntityFrameworkCore;
using Trislab.Framework.Shared.Identity.Authorization;

namespace Api.Features.Staff;

/// <summary>
/// The staff area: every endpoint under /api/admin, in one route group that requires the permission
/// <see cref="StaffPermissions.Access"/>, so a request without a sign-in answers 401 and a signed-in user without
/// the permission answers 403. Map a staff endpoint only inside the group, and never opt it out of the check with
/// <c>AllowAnonymous</c>: <c>StaffAreaTests</c> fails when one does. The group exists only when
/// <c>StaffArea:Enabled</c> is true (docs/staff-area.md).
/// </summary>
public static class StaffEndpoints
{
    public sealed record StaffSummary(int Items, int DoneItems);

    public static void MapStaffArea(this IEndpointRouteBuilder api, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!configuration.GetValue<bool>("StaffArea:Enabled"))
        {
            return;
        }

        var staff = api.MapGroup("/admin")
            .WithTags("Staff")
            .RequirePermission(StaffPermissions.Access);

        // A first staff page, over the sample's data: replace it with the product's own.
        staff.MapGet("/summary", async (AppDbContext db, CancellationToken ct) =>
            new StaffSummary(await db.Items.CountAsync(ct), await db.Items.CountAsync(i => i.Done, ct)));
    }
}
