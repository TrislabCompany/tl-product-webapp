using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Features.Health;
using Api.Features.Items;
using Api.Platform;

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
    public async Task Rejects_an_item_without_a_name()
    {
        var response = await _client.PostAsJsonAsync("/api/items", new { name = " " }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Returns_the_callers_trace_id_as_the_correlation_id()
    {
        const string traceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health/info");
        request.Headers.Add("traceparent", $"00-{traceId}-00f067aa0ba902b7-01");

        var response = await _client.SendAsync(request, Ct);

        Assert.Equal(traceId, Assert.Single(response.Headers.GetValues(PlatformObservability.CorrelationHeader)));
    }

    [Theory]
    [InlineData("POST", "/api/items")]
    [InlineData("PUT", "/api/items/999999")]
    public async Task Puts_the_correlation_id_in_a_problem_response(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { name = " " }) };

        var response = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var correlationId = Assert.Single(response.Headers.GetValues(PlatformObservability.CorrelationHeader));
        Assert.Matches("^[0-9a-f]{32}$", correlationId);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(correlationId, problem.RootElement.GetProperty("traceId").GetString());
    }
}
