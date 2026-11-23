using System.Net;
using System.Net.Http.Json;
using Api.Features.Health;
using Api.Features.Items;

namespace Integration.Tests;

public class ApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/readyz")]
    public async Task Is_alive_and_ready(string path)
    {
        var response = await _client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Answers_health_info_with_the_environment()
    {
        var info = await _client.GetFromJsonAsync<HealthInfoEndpoints.HealthInfo>("/api/health/info", Ct);

        Assert.Equal("api", info!.Name);
        Assert.Equal("test", info.Environment);
    }

    [Fact]
    public async Task Lists_the_seed_data()
    {
        var items = await _client.GetFromJsonAsync<List<Item>>("/api/items", Ct);

        Assert.Contains(items!, i => i.Name == "Read the README");
    }

    [Fact]
    public async Task Adds_changes_and_deletes_an_item()
    {
        var created = await _client.PostAsJsonAsync("/api/items", new { name = "  Write a test  " }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = (await created.Content.ReadFromJsonAsync<Item>(Ct))!;
        Assert.Equal("Write a test", item.Name);

        var changed = await _client.PutAsJsonAsync($"/api/items/{item.Id}", new { name = "Write two tests", done = true }, Ct);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.True((await changed.Content.ReadFromJsonAsync<Item>(Ct))!.Done);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync(new Uri($"/api/items/{item.Id}", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync(new Uri($"/api/items/{item.Id}", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task Refuses_a_request_without_sign_in_unless_the_endpoint_is_public()
    {
        // The framework protects every endpoint by default: the sample's items and the health info opt out.
        var response = await _client.GetAsync(new Uri("/api/v1/identity/users", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Signs_the_first_administrator_in_without_a_tenant_header()
    {
        // Single-tenant mode: the server resolves the one tenant, so sign-in takes no `tenant` header.
        var response = await _client.PostAsJsonAsync(
            "/api/v1/identity/token/issue", new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct);
        Assert.False(string.IsNullOrEmpty(token.GetProperty("accessToken").GetString()));
    }

    [Fact]
    public async Task Signs_the_demo_user_in_with_the_test_data()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/identity/token/issue", new { email = "user@example.com", password = "Password123!" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_an_item_without_a_name()
    {
        var response = await _client.PostAsJsonAsync("/api/items", new { name = " " }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
