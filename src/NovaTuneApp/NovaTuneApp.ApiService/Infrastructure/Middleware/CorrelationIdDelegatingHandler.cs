namespace NovaTuneApp.ApiService.Infrastructure.Middleware;

/// <summary>
/// Delegating handler that propagates correlation ID to outgoing HTTP requests.
/// Ensures distributed tracing context flows across service boundaries.
/// </summary>
public class CorrelationIdDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdDelegatingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Propagate correlation ID from current request to outgoing request
        if (_httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.ItemsKey] is string correlationId)
        {
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
