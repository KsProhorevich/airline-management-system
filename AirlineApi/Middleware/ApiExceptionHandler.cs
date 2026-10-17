using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace AirlineApi.Middleware;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var mapped = ExceptionProblemMapper.Map(exception);
        if (mapped.LogAsError)
        {
            _logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", httpContext.TraceIdentifier);
        }
        else
        {
            _logger.LogInformation(
                "Request rejected with status {StatusCode}. TraceId: {TraceId}",
                mapped.Status,
                httpContext.TraceIdentifier);
        }

        await Results.Problem(
            title: mapped.Title,
            detail: mapped.Detail,
            statusCode: mapped.Status,
            instance: httpContext.Request.Path,
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = httpContext.TraceIdentifier
            }).ExecuteAsync(httpContext);
        return true;
    }
}
