using System.Security.Cryptography.X509Certificates;

namespace FinanceTracker.Api.Infrastructure;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
	public async Task InvokeAsync(HttpContext context)
	{
		context.Response.Headers.XContentTypeOptions = "nosniff";
		context.Response.Headers.XFrameOptions = "DENY";
		context.Response.Headers["Referrer-Policy"] = "no-referrer";
		context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";

		await next(context: context);
	}
}

public static class SecurityHeadersMiddlewareExtensions
{
	public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
		=> app.UseMiddleware<SecurityHeadersMiddleware>();
}
