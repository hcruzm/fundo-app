using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Fundo.LoanApp.Api.Errors;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var correlationId = httpContext.TraceIdentifier;

        if (exception is BadHttpRequestException badRequest)
        {
            logger.LogInformation(
                "Malformed request body. CorrelationId: {CorrelationId}", correlationId);

            httpContext.Response.StatusCode = badRequest.StatusCode;

            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails =
                {
                    Title = "The request body could not be read.",
                    Status = badRequest.StatusCode
                }
            });
        }

        logger.LogError(exception, "Unhandled exception. CorrelationId: {CorrelationId}", correlationId);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        // The message is deliberately generic; the detail stays in the logs.
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Title = "An unexpected error occurred.",
                Status = StatusCodes.Status500InternalServerError,
                Extensions = { ["correlationId"] = correlationId }
            }
        });
    }
}
