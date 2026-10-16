using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public sealed class RequestResponseLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestResponseLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var logged = 0;

        void LogResponse()
        {
            if (Interlocked.Exchange(ref logged, 1) != 0)
            {
                return;
            }

            logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds:0.000} ms (TraceId: {TraceId})",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                context.TraceIdentifier);
        }

        context.Response.OnStarting(() =>
        {
            LogResponse();
            return Task.CompletedTask;
        });

        try
        {
            if (!context.Items.ContainsKey(TokenAuthenticationMiddleware.RejectedRequestKey))
            {
                await next(context);
            }
        }
        finally
        {
            if (context.Response.HasStarted)
            {
                LogResponse();
            }
        }
    }
}