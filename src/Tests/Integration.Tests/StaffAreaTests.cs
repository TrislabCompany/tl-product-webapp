using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Features.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Trislab.Framework.Shared.Identity.Authorization;

namespace Integration.Tests;

/// <summary>
/// The staff area under /api/admin is guarded by the framework's Identity (docs/staff-area.md): no staff endpoint
/// answers without a sign-in, and none to a user without the permission.
/// </summary>
public class StaffAreaTests(ApiFactory factory)
{
    private const string DemoPassword = "Password123!";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IEnumerable<RouteEndpoint> StaffEndpointsOf(ApiFactory factory) =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.StartsWith("/api/admin", StringComparison.Ordinal));

    [Fact]
    public void Every_staff_endpoint_requires_the_staff_permission_and_none_allows_anonymous_access()
    {
        var endpoints = StaffEndpointsOf(factory).ToList();

        Assert.NotEmpty(endpoints);
        foreach (var endpoint in endpoints)
        {
            Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
            var permissions = endpoint.Metadata.GetOrderedMetadata<IRequiredPermissionMetadata>().SelectMany(m => m.RequiredPermissions);
            Assert.Contains(StaffPermissions.Access, permissions);
        }
    }

    [Fact]
    public async Task Answers_401_to_every_staff_endpoint_without_a_sign_in()
    {
        var client = factory.CreateClient();

        foreach (var path in StaffPaths())
        {
            var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{path} answered {(int)response.StatusCode} without a sign-in");
        }
    }

    [Fact]
    public async Task Answers_403_to_a_signed_in_user_without_the_staff_permission()
    {
        var client = await SignedInAsync("user@example.com", DemoPassword);

        foreach (var path in StaffPaths())
        {
            var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{path} answered {(int)response.StatusCode} to a user without the permission");
        }
    }

    [Theory]
    [InlineData("staff@example.com", DemoPassword)]
    [InlineData(ApiFactory.AdminEmail, ApiFactory.AdminPassword)]
    public async Task Lets_a_staff_user_and_an_administrator_in(string email, string password)
    {
        var client = await SignedInAsync(email, password);

        var summary = await client.GetFromJsonAsync<StaffEndpoints.StaffSummary>("/api/admin/summary", Ct);

        Assert.True(summary!.Items >= 3);
    }

    // The path of every staff endpoint without a route parameter, read from the running app.
    private IEnumerable<string> StaffPaths() =>
        StaffEndpointsOf(factory).Select(e => e.RoutePattern.RawText!).Where(p => !p.Contains('{', StringComparison.Ordinal));

    private async Task<HttpClient> SignedInAsync(string email, string password)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/identity/token/issue", new { email, password }, Ct);
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
