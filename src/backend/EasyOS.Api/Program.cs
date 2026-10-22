using System.Threading.RateLimiting;
using EasyOS.Api;
using EasyOS.Api.Auth;
using EasyOS.Api.Common;
using EasyOS.Api.MultiTenancy;
using EasyOS.Api.Platform;
using EasyOS.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandler>();
builder.Services.AddOpenApi();

builder.Services.AddEasyOSInfrastructure(builder.Configuration);
builder.Services.Configure<MultiTenancyOptions>(builder.Configuration.GetSection("MultiTenancy"));
builder.Services.Configure<PlatformOptions>(builder.Configuration.GetSection("Platform"));
builder.Services.AddEasyOSAuth(builder.Environment);
builder.Services.AddEasyOSFeatures();

var loginLimit = builder.Configuration.GetValue("Security:LoginAttemptsPerMinute", 10);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
        $"{http.Request.Host.Host}|{http.Connection.RemoteIpAddress}",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginLimit, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    // A aplicação roda atrás do Caddy na mesma VPS (rede interna do Docker).
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

await app.Services.InitializeCatalogAsync();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapInternalEndpoints();

app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<CsrfHeaderMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapAuthEndpoints();
app.MapPlatformEndpoints();
app.MapEasyOSFeatures();

// Frontend React (build copiado para wwwroot). Rotas desconhecidas fora de /api voltam para o index.html.
app.UseDefaultFiles();
app.UseStaticFiles();
// Rotas /api inexistentes devolvem 404 (e não o index.html do SPA).
app.MapFallback("/api/{**rest}", () => Results.NotFound()).AllowAnonymous();
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

public partial class Program;
