namespace EasyOS.Api.Common;

/// <summary>
/// Exige o header X-Requested-With nas chamadas que alteram dados. Um site de terceiros não
/// consegue enviá-lo sem preflight CORS (que não é liberado), o que bloqueia CSRF. Os cookies
/// SameSite=Strict são uma segunda barreira.
/// </summary>
public sealed class CsrfHeaderMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Requested-With";
    public const string HeaderValue = "easyos";

    public Task InvokeAsync(HttpContext http)
    {
        var isUnsafe = !(HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method) || HttpMethods.IsOptions(http.Request.Method));
        if (isUnsafe && http.Request.Path.StartsWithSegments("/api") &&
            !string.Equals(http.Request.Headers[HeaderName], HeaderValue, StringComparison.Ordinal))
        {
            http.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Results.Problem(title: "Requisição recusada", detail: "Cabeçalho de segurança ausente.", statusCode: 403).ExecuteAsync(http);
        }
        return next(http);
    }
}
