using HotChocolate;
using ShiftWise.Api.Common;

namespace ShiftWise.Api.GraphQL;

// REST turns AppException into ProblemDetails; GraphQL needs it as an error
// with a readable message and a machine-readable code instead of the default
// "Unexpected Execution Error".
public class AppExceptionErrorFilter : IErrorFilter
{
    public IError OnError(IError error)
    {
        if (error.Exception is not AppException ex)
        {
            return error;
        }

        var code = ex.StatusCode switch
        {
            StatusCodes.Status400BadRequest => "BAD_REQUEST",
            StatusCodes.Status401Unauthorized => "UNAUTHENTICATED",
            StatusCodes.Status403Forbidden => "FORBIDDEN",
            StatusCodes.Status404NotFound => "NOT_FOUND",
            StatusCodes.Status409Conflict => "CONFLICT",
            _ => "ERROR"
        };

        return error
            .WithMessage(ex.Message)
            .WithCode(code)
            .SetExtension("status", ex.StatusCode)
            .RemoveException();
    }
}
