using Microsoft.AspNetCore.Diagnostics;
using ParcelTracking.Domain.Exceptions;

namespace ParcelTracking.Api.ExceptionHandlers;

public class TerminalStateExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ParcelInTerminalStateException terminalException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        httpContext.Response.ContentType = "application/json";

        var errorResponse = new
        {
            error = "terminal_state",
            message = terminalException.Message,
            parcelId = terminalException.ParcelId,
            currentStatus = terminalException.Status.ToString()
        };

        await httpContext.Response.WriteAsJsonAsync(errorResponse, cancellationToken);

        return true;
    }
}
