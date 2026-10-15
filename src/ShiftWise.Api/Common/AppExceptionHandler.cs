using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ShiftWise.Api.Common;

public class AppExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Anything that isn't ours falls through to the default 500 handling.
        if (exception is not AppException appException)
        {
            return false;
        }

        var problem = new ProblemDetails
        {
            Status = appException.StatusCode,
            Title = appException.Message
        };

        httpContext.Response.StatusCode = appException.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
