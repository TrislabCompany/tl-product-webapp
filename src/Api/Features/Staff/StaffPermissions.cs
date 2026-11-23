using Trislab.Framework.Shared.Constants;

namespace Api.Features.Staff;

/// <summary>
/// The permission that opens the staff area. The product registers it at start-up, in the API and in the
/// migrator, before the modules: the built-in Admin role gets it with every other non-root permission, and the
/// migrator's `Staff` role holds only this one (docs/staff-area.md).
/// </summary>
public static class StaffPermissions
{
    /// <summary>The permission as the token and the role claims name it: <c>Permissions.Staff.Access</c>.</summary>
    public static readonly string Access = TrislabPermission.NameFor("Access", "Staff");

    public static readonly TrislabPermission AccessPermission = new("Use the staff area", "Access", "Staff");

    private static readonly object Gate = new();
    private static bool _registered;

    /// <summary>
    /// Registers the permission once per process. The framework's registry is a plain list: the API, the migrator
    /// and test hosts that start side by side must not add to it at the same time.
    /// </summary>
    public static void Register()
    {
        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            PermissionConstants.Register([AccessPermission]);
            _registered = true;
        }
    }
}
