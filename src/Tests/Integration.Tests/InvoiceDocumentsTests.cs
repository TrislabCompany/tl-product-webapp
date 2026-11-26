using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Integration.Tests;

/// <summary>
/// InvoiceDocuments with its Slovenian rule pack, registered in single-tenant mode, after the real migrator and its
/// demo data (src/Migrator/InvoiceDocumentsDemoData.cs). The module's own rules are tested in tl-framework; this
/// checks that the starter wires it: the routes answer, the demo documents are there in each state, a PDF renders,
/// and a user without the permissions gets nothing.
/// </summary>
public class InvoiceDocumentsTests(ApiFactory factory)
{
    private const string Documents = "/api/v1/invoice-documents/documents";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_administrator_sees_the_demo_documents_in_every_state()
    {
        var client = await SignedInAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword);

        var page = await client.GetFromJsonAsync<JsonElement>($"{Documents}?pageSize=50", Ct);
        var rows = page.GetProperty("items").EnumerateArray().ToList();

        string[] Pairs(JsonElement r) => [r.GetProperty("type").ToString(), r.GetProperty("status").ToString()];
        var pairs = rows.Select(r => string.Join('/', Pairs(r))).ToList();
        Assert.Contains(pairs, p => p is "ProForma/Issued" or "1/1");
        Assert.Contains(pairs, p => p is "Invoice/Paid" or "2/2");
        Assert.Contains(pairs, p => p is "Invoice/Issued" or "2/1");
        Assert.Contains(pairs, p => p is "CreditNote/Issued" or "3/1");
        Assert.Contains(pairs, p => p is "Invoice/Draft" or "2/0");
        Assert.All(
            rows.Where(r => r.GetProperty("number").ValueKind == JsonValueKind.String),
            r => Assert.Matches("^(PRE|RAC|DOB)-\\d{4}-\\d{5}$", r.GetProperty("number").GetString()!));
    }

    [Fact]
    public async Task An_issued_invoice_prints_as_a_pdf()
    {
        var client = await SignedInAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword);
        var page = await client.GetFromJsonAsync<JsonElement>($"{Documents}?pageSize=50", Ct);
        var issued = page.GetProperty("items").EnumerateArray()
            .First(r => r.GetProperty("number").ValueKind == JsonValueKind.String);

        var response = await client.GetAsync(new Uri($"{Documents}/{issued.GetProperty("id").GetGuid()}/pdf", UriKind.Relative), Ct);

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }

    [Fact]
    public async Task A_user_without_the_permissions_gets_403_and_no_one_signed_out_gets_in()
    {
        var user = await SignedInAsync("user@example.com", "Password123!");
        var anonymous = factory.CreateClient();

        var forbidden = await user.GetAsync(new Uri(Documents, UriKind.Relative), Ct);
        var unauthorized = await anonymous.GetAsync(new Uri(Documents, UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
    }

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
