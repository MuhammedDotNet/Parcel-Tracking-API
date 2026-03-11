using System.Diagnostics;

namespace ParcelTracking.Api.Middleware;

/// <summary>
/// Middleware that logs HTTP requests with correlation IDs and timing information.
/// Ensures no sensitive data (auth headers, bodies) is logged.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Generate unique correlation ID for this request
        var requestId = Guid.NewGuid().ToString("N")[..8];
        
        // Add X-Request-Id header to response
        context.Response.Headers["X-Request-Id"] = requestId;

        // Measure request duration
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Log method, path, status code, duration, and correlation ID
            // Explicitly avoid logging sensitive data like Authorization headers or bodies
            _logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {Elapsed}ms [RequestId: {RequestId}]",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                requestId);
        }
    }
}
