using System.Text.Json;

namespace WalletFlow.WebApi.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Erro não tratado ao processar a requisição {TraceId}.", context.TraceIdentifier);
            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await JsonSerializer.SerializeAsync(context.Response.Body, new
            {
                type = "about:blank",
                title = "Erro interno do servidor",
                status = StatusCodes.Status500InternalServerError,
                detail = "Ocorreu um erro inesperado ao processar a solicitação.",
                traceId = context.TraceIdentifier
            }, cancellationToken: context.RequestAborted);
        }
    }
}
