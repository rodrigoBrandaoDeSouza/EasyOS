using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EasyOS.Tests.Infrastructure;

namespace EasyOS.Tests;

[Collection(nameof(TestAppCollection))]
public class FeatureTests(TestAppFixture fixture)
{
    private readonly TestApp _app = fixture.App;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<HttpClient> Admin() => await _app.LoginAsync(TestApp.HostA, "admin@cliente.com");

    private static object NewCustomer(string name, string email) => new
    {
        customer = new
        {
            tradingName = name, companyName = name + " LTDA", cpfCnpj = "12.345.678/0001-90",
            address = "Rua X", number = 10, complement = "", neighborhood = "Centro", city = "Niterói", state = "RJ", zipcode = "24000-000",
            phone = "2122223333", contactName = "Fulano", certificateDate = 30, clientHasContract = true,
        },
        user = new { name = "Gerente " + name, email, department = "Adm", profile = 1, password = "segredo1" },
    };

    private async Task<int> CreateCustomer(HttpClient admin, string name)
    {
        var email = $"{Guid.NewGuid():N}@cliente.com";
        var response = await admin.PostAsJsonAsync("/api/customers", NewCustomer(name, email));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private async Task<int> UserId(HttpClient admin, string email)
    {
        var users = await admin.GetFromJsonAsync<JsonElement>("/api/company-users");
        return users.EnumerateArray().First(u => u.GetProperty("email").GetString() == email).GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Customer_user_only_sees_own_customer()
    {
        var admin = await Admin();
        var otherCustomer = await CreateCustomer(admin, "Outro Cliente");

        var customerClient = await _app.LoginAsync(TestApp.HostA, "cliente@restaurante.com");
        var me = await customerClient.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var ownId = me.GetProperty("customerId").GetInt32();

        Assert.Equal(HttpStatusCode.OK, (await customerClient.GetAsync($"/api/customers/{ownId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await customerClient.GetAsync($"/api/customers/{otherCustomer}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await customerClient.GetAsync($"/api/customers/{otherCustomer}/reports/plagues-found")).StatusCode);
        // Listagem geral de clientes é exclusiva da empresa.
        Assert.Equal(HttpStatusCode.Forbidden, (await customerClient.GetAsync("/api/customers")).StatusCode);
    }

    [Fact]
    public async Task Back_office_cannot_manage_users()
    {
        var backoffice = await _app.LoginAsync(TestApp.HostA, "backoffice@cliente.com");
        var response = await backoffice.PostAsJsonAsync("/api/company-users", new { name = "X", email = "x@x.com", department = "Y", profile = 2, password = "123456" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Visit_lifecycle_and_manual_order_with_sequence()
    {
        var admin = await Admin();
        var customerId = await CreateCustomer(admin, "Padaria Visita");
        var adminId = await UserId(admin, "admin@cliente.com");
        var technicianId = await UserId(admin, "tecnico@cliente.com");

        var visitDate = DateTime.Today.AddDays(-1).AddHours(14);
        var create = await admin.PostAsJsonAsync("/api/visits", new
        {
            customerId, technicianId, responsibleId = adminId, visitDate, offsetMinutes = -180, description = "Dedetização geral",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var visitId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        // A visita aparece no calendário e na lista do dia, no horário local informado.
        var calendar = await admin.GetFromJsonAsync<JsonElement>($"/api/visits/calendar?year={visitDate.Year}");
        Assert.Contains(calendar.EnumerateArray(), v => v.GetProperty("id").GetInt32() == visitId);
        var day = DateOnly.FromDateTime(visitDate).ToString("yyyy-MM-dd");
        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/visits?from={day}&to={day}");
        var item = list.GetProperty("items").EnumerateArray().Single(v => v.GetProperty("id").GetInt32() == visitId);
        Assert.Equal(visitDate, item.GetProperty("visitDate").GetDateTime());
        Assert.Equal("pendente", item.GetProperty("status").GetString());

        var pending = await admin.GetFromJsonAsync<JsonElement>($"/api/visits/pending-for-order?customerId={customerId}");
        Assert.Contains(pending.EnumerateArray(), v => v.GetProperty("id").GetInt32() == visitId);

        // Duas OS seguidas recebem números consecutivos; a primeira encerra a visita com check-in.
        var orderDate = DateOnly.FromDateTime(visitDate).ToString("yyyy-MM-dd");
        object Order(int visit, int hour) => new
        {
            customerId, visitId = visit, technicianId, orderDate,
            timeStart = visitDate.Date.AddHours(hour), timeEnd = visitDate.Date.AddHours(hour + 1), offsetMinutes = -180,
            serviceExecuted = true, responsibleName = "Gerente", responsibleBusiness = "Gerência", observations = "Tudo certo",
            details = new
            {
                hadEvidenceOfPlague = true, hadEquipmentRevision = true, hadBaiting = true, hadPlaquesOfGlue = true,
                hadLuminousTrap = true, customerNeedPlagueAccount = true, hadPheromoneTrap = true, hadProductConsumption = false,
            },
            plagues = new[] { new { plagueId = 2, plagueStatusId = 1, location = "Cozinha", quantity = 3 } },
            baitings = new[] { new { type = "Roída", point = "1", quantity = 0 }, new { type = "instaladas", point = "", quantity = 10 } },
            plaquesOfGlue = new[] { new { type = "capturado", point = "2", quantity = 0, rodentQuantity = 1 } },
            pheromones = new[] { new { type = "Traça", point = "A", quantity = 4 } },
            luminous = new[] { new { point = "L1", quantity = 0, plagueCounts = new[] { new { plagueId = 1, quantity = 5 }, new { plagueId = 3, quantity = 2 } } } },
            nonConformities = new[] { new { place = "Depósito", description = "Frestas", suggestion = "Vedar" } },
            evidences = new[] { "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==" },
        };

        var first = await admin.PostAsJsonAsync("/api/orders", Order(visitId, 14));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var second = await admin.PostAsJsonAsync("/api/orders", Order(0, 16));
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(firstBody.GetProperty("sequence").GetInt64() + 1, secondBody.GetProperty("sequence").GetInt64());

        // Reenvio idêntico não duplica a OS.
        var repeat = await (await admin.PostAsJsonAsync("/api/orders", Order(0, 16))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(secondBody.GetProperty("id").GetInt32(), repeat.GetProperty("id").GetInt32());

        var visit = await admin.GetFromJsonAsync<JsonElement>($"/api/visits/{visitId}");
        Assert.Equal("realizada", visit.GetProperty("status").GetString());
        Assert.Equal(visitDate, visit.GetProperty("checkinDate").GetDateTime());

        var orderId = firstBody.GetProperty("id").GetInt32();
        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}");
        Assert.Equal(visitDate, detail.GetProperty("timeStart").GetDateTime());
        Assert.Single(detail.GetProperty("evidenceIds").EnumerateArray());
        var luminous = detail.GetProperty("luminous")[0].GetProperty("plagueCounts");
        Assert.Equal("Mosca", luminous[0].GetProperty("plagueName").GetString());

        var evidenceId = detail.GetProperty("evidenceIds")[0].GetInt32();
        var image = await admin.GetAsync($"/api/orders/{orderId}/evidences/{evidenceId}");
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);

        // Relatórios do cliente refletem a OS lançada.
        var from = DateOnly.FromDateTime(visitDate).AddMonths(-1).ToString("yyyy-MM-dd");
        var to = DateOnly.FromDateTime(visitDate).ToString("yyyy-MM-dd");
        foreach (var type in new[] { "product-consumption", "input-consumption", "plagues-found", "product-plague", "pheromone-traps", "luminous-traps", "rodents-dead", "glue-traps", "gnawed-baits", "rodents-rodenticide" })
        {
            var report = await admin.GetAsync($"/api/customers/{customerId}/reports/{type}?from={from}&to={to}");
            Assert.True(report.IsSuccessStatusCode, $"{type}: {await report.Content.ReadAsStringAsync()}");
        }
        var plagues = await admin.GetFromJsonAsync<JsonElement>($"/api/customers/{customerId}/reports/plagues-found?from={from}&to={to}");
        Assert.Equal(6, plagues.GetProperty("series")[0].GetProperty("data").EnumerateArray().Sum(d => d.GetDouble()));
        var lum = await admin.GetFromJsonAsync<JsonElement>($"/api/customers/{customerId}/reports/luminous-traps?from={from}&to={to}");
        Assert.Equal(14, lum.GetProperty("series")[0].GetProperty("data").EnumerateArray().Sum(d => d.GetDouble()));
        var nc = await admin.GetFromJsonAsync<JsonElement>($"/api/customers/{customerId}/reports/non-conformities?from={from}&to={to}");
        Assert.Equal(2, nc.GetArrayLength());

        Assert.True((await admin.GetAsync($"/api/customers/{customerId}/dashboard?from={from}&to={to}")).IsSuccessStatusCode);
        Assert.True((await admin.GetAsync($"/api/dashboard/company?from={from}&to={to}")).IsSuccessStatusCode);
        var productivity = await admin.GetFromJsonAsync<JsonElement>($"/api/reports/productivity?from={from}&to={to}");
        Assert.Contains(productivity.EnumerateArray(), p => p.GetProperty("accomplished").GetInt32() >= 2);

        var certificates = await admin.GetFromJsonAsync<JsonElement>($"/api/customers/{customerId}/certificates");
        Assert.Equal(2, certificates.GetArrayLength());
        var certificate = await admin.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}/certificate");
        Assert.Equal(30, certificate.GetProperty("validityDays").GetInt32());

        // Cancelar uma OS a remove dos relatórios.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/orders/{orderId}")).StatusCode);
        var after = await admin.GetFromJsonAsync<JsonElement>($"/api/customers/{customerId}/reports/plagues-found?from={from}&to={to}");
        Assert.Equal(3, after.GetProperty("series")[0].GetProperty("data").EnumerateArray().Sum(d => d.GetDouble()));
    }

    [Fact]
    public async Task Visit_with_checkin_cannot_be_deleted_and_future_visit_can()
    {
        var admin = await Admin();
        var customerId = await CreateCustomer(admin, "Mercado");
        var adminId = await UserId(admin, "admin@cliente.com");

        var create = await admin.PostAsJsonAsync("/api/visits", new { customerId, responsibleId = adminId, visitDate = DateTime.Today.AddDays(3).AddHours(9), offsetMinutes = -180 });
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var visit = await admin.GetFromJsonAsync<JsonElement>($"/api/visits/{id}");
        Assert.Equal("não confirmada", visit.GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/visits/{id}")).StatusCode);
        Assert.Equal("cancelada", (await admin.GetFromJsonAsync<JsonElement>($"/api/visits/{id}")).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Files_upload_list_download_and_delete()
    {
        var admin = await Admin();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync("/api/files/folder", new { name = "Licenças" })).StatusCode);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent("conteúdo"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "files", "alvara.pdf");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync("/api/files/upload?path=Licenças", form)).StatusCode);

        var root = await admin.GetFromJsonAsync<JsonElement>("/api/files");
        Assert.Contains(root.GetProperty("folders").EnumerateArray(), f => f.GetProperty("name").GetString() == "Licenças");

        // Usuário do cliente consegue listar e baixar, mas não enviar.
        var customer = await _app.LoginAsync(TestApp.HostA, "cliente@restaurante.com");
        var folder = await customer.GetFromJsonAsync<JsonElement>("/api/files?path=Licenças");
        Assert.Equal("alvara.pdf", folder.GetProperty("files")[0].GetProperty("name").GetString());
        var download = await customer.GetAsync("/api/files/download?path=Licenças/alvara.pdf");
        Assert.Equal("conteúdo", await download.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.DeleteAsync("/api/files?path=Licenças")).StatusCode);

        // Os arquivos de um tenant não aparecem no outro.
        var otherTenant = await _app.LoginAsync(TestApp.HostB, "admin@cliente.com");
        var otherRoot = await otherTenant.GetFromJsonAsync<JsonElement>("/api/files");
        Assert.Empty(otherRoot.GetProperty("folders").EnumerateArray());

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/files?path=../../tenant-b")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync("/api/files?path=Licenças")).StatusCode);
    }

    [Fact]
    public async Task Catalog_products_and_plagues()
    {
        var admin = await Admin();
        var create = await admin.PostAsJsonAsync("/api/plagues", new { name = "Escorpiões", plagueType = "Aracnídeo" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var plagueId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var types = await admin.GetFromJsonAsync<JsonElement>("/api/product-types");
        var typeId = types[0].GetProperty("id").GetInt32();

        var product = await admin.PostAsJsonAsync("/api/products", new
        {
            name = "Produto X", productTypeId = typeId, productCode = 1, chemicalGroup = "Piretróide", activeIngredient = "Cipermetrina",
            plagues = new[] { new { plagueId, concentration = 0.5 } },
            applications = new[] { new { description = "Pulverizador manual" } },
            activeIngredients = new[] { new { activeIngredient = "Cipermetrina", concentration = 20 } },
        });
        Assert.Equal(HttpStatusCode.Created, product.StatusCode);
        var productId = (await product.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var byPlague = await admin.GetFromJsonAsync<JsonElement>($"/api/products/by-plague/{plagueId}");
        Assert.Single(byPlague.EnumerateArray());
        Assert.Equal("Pulverizador manual", byPlague[0].GetProperty("applications")[0].GetProperty("description").GetString());

        // Remover a praga do produto desativa o vínculo.
        await admin.PutAsJsonAsync($"/api/products/{productId}", new { name = "Produto X", productTypeId = typeId, productCode = 1, plagues = Array.Empty<object>() });
        Assert.Empty((await admin.GetFromJsonAsync<JsonElement>($"/api/products/by-plague/{plagueId}")).EnumerateArray());
    }
}
