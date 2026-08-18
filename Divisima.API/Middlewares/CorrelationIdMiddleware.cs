namespace Divisima.API.Middlewares
{
    // Açıklayıcı yorum: Her isteğe benzersiz correlation_id atar (header'da yoksa üretir).
    // Serilog LogContext'e eklenir - bir isteğin/siparişin tüm logları tek id ile izlenir.
    public class CorrelationIdMiddleware
    {
        private const string HeaderName = "X-Correlation-Id";
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing)
                ? existing.ToString()
                : Guid.NewGuid().ToString();

            context.Response.Headers[HeaderName] = correlationId;
            // Açıklayıcı yorum: Serilog LogContext'e push - bu istekteki tüm loglara eklenir
            using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }
    }
}
