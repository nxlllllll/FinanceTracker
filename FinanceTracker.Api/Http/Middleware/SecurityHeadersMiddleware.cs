namespace FinanceTracker.Api.Http.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
	private const string ApiPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";

	private const string DocumentationPolicy =
		"default-src 'self'; " +
		"script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
		"style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
		"font-src 'self' data: https://fonts.gstatic.com https://cdn.jsdelivr.net; " +
		"img-src 'self' data: https:; " +
		"connect-src 'self'; " +
		"frame-ancestors 'none'; " +
		"base-uri 'self'";

	public async Task InvokeAsync(HttpContext context)
	{
		context.Response.Headers.XContentTypeOptions = "nosniff";
		context.Response.Headers.XFrameOptions = "DENY";
		context.Response.Headers["Referrer-Policy"] = "no-referrer";
		context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";

		bool isDocumentation = context.Request.Path.StartsWithSegments(other: "/scalar") || context.Request.Path.StartsWithSegments(other: "/openapi");

		context.Response.Headers.ContentSecurityPolicy = isDocumentation ? DocumentationPolicy : ApiPolicy;

		await next(context: context);
	}
}

public static class SecurityHeadersMiddlewareExtensions
{
	public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
		=> app.UseMiddleware<SecurityHeadersMiddleware>();
}
