using NovaTuneApp.ApiService.Exceptions;

namespace NovaTuneApp.ApiService.Infrastructure;

/// <summary>
/// Endpoint filter that handles UploadException and converts them to Problem Details responses.
/// </summary>
public class UploadExceptionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (UploadException ex)
        {
            var httpContext = context.HttpContext;
            return TypedResults.Problem(UploadProblemDetailsFactory.Create(ex, httpContext));
        }
    }
}
