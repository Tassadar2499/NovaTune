using NovaTuneApp.ApiService.Exceptions;

namespace NovaTuneApp.ApiService.Infrastructure;

/// <summary>
/// Endpoint filter that handles AuthException and converts them to Problem Details responses.
/// Eliminates repetitive try/catch blocks in auth endpoints.
/// </summary>
public class AuthExceptionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (AuthException ex)
        {
            var httpContext = context.HttpContext;
            return TypedResults.Problem(AuthProblemDetailsFactory.Create(ex, httpContext));
        }
    }
}
