using ErrorOr;

namespace Barrelo.Api.Common;

public static class ErrorOrExtensions
{
    public static IResult ToProblem(this List<Error> errors)
    {
        if (errors.Count == 0)
            return Results.Problem();

        var statusCode = errors[0].Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };

        return Results.Problem(
            statusCode: statusCode,
            title: errors[0].Description,
            extensions: new Dictionary<string, object?>
            {
                ["errors"] = errors.Select(e => new { e.Code, e.Description }),
            });
    }
}
