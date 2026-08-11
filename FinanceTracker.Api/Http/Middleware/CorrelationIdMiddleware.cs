using FinanceTracker.Core.Observability.Correlation;

namespace FinanceTracker.Api.Http.Middleware;

/// <summary>Establishes the request's correlation ID</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
	public const string HeaderName = "X-Correlation-Id";

	public async Task InvokeAsync(
		HttpContext context,
		ICorrelationContext correlationContext)
	{
		Guid correlationId = ResolveCorrelationId(context: context);
		correlationContext.Set(correlationId: correlationId);

		context.Response.OnStarting(callback: () =>
		{
			context.Response.Headers[HeaderName] = correlationId.ToString();
			return Task.CompletedTask;
		});

		await next(context: context);
	}

	private static Guid ResolveCorrelationId(HttpContext context)
	{
		string headerValue = context.Request.Headers[HeaderName].ToString();

		if (!String.IsNullOrEmpty(value: headerValue) && Guid.TryParse(input: headerValue, result: out Guid parsed) && parsed != Guid.Empty)
			return parsed;

		return Guid.CreateVersion7();
	}
}

public static class CorrelationIdMiddlewareExtensions
{
	public static IApplicationBuilder UseCorrelationIdMiddleware(this IApplicationBuilder app)
		=> app.UseMiddleware<CorrelationIdMiddleware>();
}
