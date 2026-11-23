# The staff area

An optional `/admin` area for staff, in the same app and on the same hostname as the public part. Use it when an app has a public part with no sign-in, such as a wizard anyone may fill in, but also needs a few pages only staff may see: the list of submissions, a re-send button. An app with no public part signs every user in and needs no separate staff area. `init-product` keeps it only when the product asks for it (`--features staff-area`); in the starter it is built in and, at run time, off until it is switched on ([section 2](#2-turn-it-on)).

The staff area is protected by the app itself, through the framework's Identity module in single-tenant mode. There is no second hostname, no second app and no Cloudflare Access in front of production: signing in, not the hostname, protects the endpoints, so a routing mistake cannot expose them. In the sandbox, the platform's Zero Trust sign-in still sits in front of the whole hostname, as it does for every sandbox.

## 1. How it works

- **The API.** Every staff endpoint is under `/api/admin`, in one route group (`src/Api/Features/Staff/StaffEndpoints.cs`) that requires the permission `Permissions.Staff.Access`. A request with no sign-in answers 401, and a signed-in user without the permission answers 403. The group is added only when `StaffArea:Enabled` is `true`; otherwise `/api/admin` answers 404.
- **The permission and the role.** The product registers the permission at start-up (`StaffPermissions`). The built-in `Admin` role gets every non-root permission, so administrators are staff. The migrator also creates a custom role, `Staff`, with only that permission (`src/Migrator/StaffRoleSeeder.cs`), for people who must see the staff pages and nothing else. An administrator puts a user in the role through Identity's role endpoints, or in the framework's admin screens.
- **The front end.** `/admin` is a route of `web/src/features/admin/`. It shows a sign-in form, calls `POST /api/v1/identity/token/issue` (no tenant header in single-tenant mode), keeps the access token for the browser tab only (`sessionStorage`), and sends it as a bearer token to `/api/admin/...`. The public pages never call a staff endpoint. The route exists only when `PUBLIC_STAFF_AREA` is `true`.
- **The test.** `StaffAreaTests` lists every endpoint under `/api/admin` from the running app and fails when one allows anonymous access or lacks the permission, and calls each one without a token, with a user without the permission, and with a staff user.

## 2. Turn it on

| Setting | Where | Meaning |
| --- | --- | --- |
| `StaffArea__Enabled` | `apps[].env` of `api` | `true` maps `/api/admin`. Default `false` |
| `PUBLIC_STAFF_AREA` | `apps[].env` of `web` | `true` adds the `/admin` route to the front end. Default off |

Members of the staff are users of the app with the role `Staff` or `Admin`. The first administrator is seeded by the migrator ([configuration.md](configuration.md#2a-tenancy-mode)); it adds the others.

## 3. Add a staff page

1. Add the endpoint to `StaffEndpoints.cs`, inside the group: it inherits the permission. Never map a staff endpoint outside the group, and never add `.AllowAnonymous()` to it: the test fails.
2. Add the page under `web/src/features/admin/`, call it through `api.ts`, and give every text in both languages (`web/src/locales/en-US/admin.json` and `sl-SI/`).
3. Add an integration test that calls it as staff and as someone who is not.

## 4. What it does not do

- **Entra ID sign-in.** Staff sign in with the app's own accounts, e-mail and password, with the framework's two-factor sign-in available. Single sign-on with Entra ID is a change in `tl-framework`'s Identity module; the staff area needs no change when it arrives.
- **Replace the platform's sandbox sign-in.** Cloudflare Access stays in front of the sandbox hostnames only.
