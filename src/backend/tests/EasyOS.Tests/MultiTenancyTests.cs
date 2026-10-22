using System.Net;
using System.Net.Http.Json;
using EasyOS.Api.Auth;
using EasyOS.Tests.Infrastructure;

namespace EasyOS.Tests;

[Collection(nameof(TestAppCollection))]
public class MultiTenancyTests(TestAppFixture fixture)
{
    private readonly TestApp _app = fixture.App;

    [Fact]
    public async Task Unknown_domain_returns_404()
    {
        var client = _app.ClientFor("nao-cadastrado.test");
        var response = await client.GetAsync("/api/tenant/branding");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Branding_comes_from_the_domain_tenant()
    {
        var a = await _app.ClientFor(TestApp.HostA).GetFromJsonAsync<BrandingDto>("/api/tenant/branding");
        var b = await _app.ClientFor(TestApp.HostB).GetFromJsonAsync<BrandingDto>("/api/tenant/branding");
        Assert.Equal("Tenant A", a!.Name);
        Assert.Equal("Tenant B", b!.Name);
    }

    [Fact]
    public async Task Same_email_logs_into_the_database_of_the_accessed_domain()
    {
        var clientA = await _app.LoginAsync(TestApp.HostA, "admin@cliente.com");
        var clientB = await _app.LoginAsync(TestApp.HostB, "admin@cliente.com");

        var meA = await clientA.GetFromJsonAsync<MeResponse>("/api/auth/me");
        var meB = await clientB.GetFromJsonAsync<MeResponse>("/api/auth/me");

        Assert.Equal("Admin A", meA!.Name);
        Assert.Equal("Admin B", meB!.Name);
    }

    [Fact]
    public async Task Session_cookie_from_one_tenant_is_rejected_on_another()
    {
        var clientA = _app.ClientFor(TestApp.HostA);
        var login = await clientA.PostAsJsonAsync("/api/auth/login", new { email = "admin@cliente.com", password = TestApp.Password });
        var cookie = login.Headers.GetValues("Set-Cookie").First().Split(';')[0];

        // Mesmo cookie apresentado no domínio do tenant B (ex.: cookie roubado ou domínio pai compartilhado).
        var raw = _app.CreateClient(new() { BaseAddress = new Uri($"http://{TestApp.HostB}"), HandleCookies = false });
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("Cookie", cookie);
        var response = await raw.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("tecnico@cliente.com", TestApp.Password, "permissão")]
    [InlineData("inativo@cliente.com", TestApp.Password, "inválidos")]
    [InlineData("admin@cliente.com", "senha-errada", "inválidos")]
    public async Task Login_rules_from_legacy_are_kept(string email, string password, string expectedMessage)
    {
        var response = await _app.ClientFor(TestApp.HostA).PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedMessage, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Customer_user_can_log_in()
    {
        var client = await _app.LoginAsync(TestApp.HostA, "cliente@restaurante.com");
        var me = await client.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.Equal(UserTypes.Customer, me!.Type);
        Assert.NotNull(me.CustomerId);
    }

    [Fact]
    public async Task Mutations_without_csrf_header_are_refused()
    {
        var client = _app.CreateClient(new() { BaseAddress = new Uri($"http://{TestApp.HostA}") });
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@cliente.com", password = TestApp.Password });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Platform_api_is_only_available_on_platform_host()
    {
        var onTenant = await _app.ClientFor(TestApp.HostA).PostAsJsonAsync("/api/platform/auth/login", new { email = "admin@easyos.com.br", password = TestApp.PlatformPassword });
        Assert.Equal(HttpStatusCode.NotFound, onTenant.StatusCode);

        var platform = _app.ClientFor(TestApp.PlatformHost);
        var login = await platform.PostAsJsonAsync("/api/platform/auth/login", new { email = "admin@easyos.com.br", password = TestApp.PlatformPassword });
        login.EnsureSuccessStatusCode();
        var tenants = await platform.GetFromJsonAsync<List<TenantListDto>>("/api/platform/tenants");
        Assert.Contains(tenants!, t => t.Slug == "tenant-a");
    }

    [Fact]
    public async Task Tls_check_only_accepts_registered_domains()
    {
        var client = _app.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/internal/tls-check?domain={TestApp.HostA}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/internal/tls-check?domain=evil.test")).StatusCode);
    }

    private sealed record BrandingDto(string Name, string PrimaryColor);
    private sealed record TenantListDto(Guid Id, string Slug, string Name);
}
