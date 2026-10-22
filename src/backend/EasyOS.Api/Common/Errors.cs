using EasyOS.Infrastructure.MultiTenancy;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EasyOS.Api.Common;

/// <summary>Erro de regra de negócio: a mensagem é exibida ao usuário (400).</summary>
public class BusinessException(string message) : Exception(message);

/// <summary>Registro inexistente ou fora do escopo do usuário (404).</summary>
public sealed class NotFoundException(string message = "Registro não encontrado.") : Exception(message);

/// <summary>Usuário autenticado sem permissão para a operação (403).</summary>
public sealed class ForbiddenException(string message = "Você não tem permissão para esta operação.") : Exception(message);

public sealed class ExceptionHandler(ILogger<ExceptionHandler> logger, IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = exception switch
        {
            BusinessException e => (StatusCodes.Status400BadRequest, "Não foi possível concluir a operação", e.Message),
            NotFoundException e => (StatusCodes.Status404NotFound, "Não encontrado", e.Message),
            ForbiddenException e => (StatusCodes.Status403Forbidden, "Acesso negado", e.Message),
            TenantNotResolvedException => (StatusCodes.Status404NotFound, "Domínio não cadastrado", "Este endereço não está associado a nenhum cliente EasyOS."),
            BadHttpRequestException e => (StatusCodes.Status400BadRequest, "Requisição inválida", e.Message),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno", "Ocorreu um erro inesperado. Tente novamente em instantes."),
        };

        if (status >= 500)
            logger.LogError(exception, "Erro não tratado em {Method} {Path}", http.Request.Method, http.Request.Path);

        http.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
        });
    }
}
