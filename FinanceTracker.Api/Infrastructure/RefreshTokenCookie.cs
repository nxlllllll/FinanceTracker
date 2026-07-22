using FinanceTracker.Infrastructure.Services.Token;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Api.Infrastructure;

/// <summary>The single owner of the refresh-token cookie contract: name, flags, lifetime.</summary>
public static class RefreshTokenCookie
{
	private const string Name = "__Host-refresh-token";

	public static void Append(HttpContext httpContext, string refreshToken, IOptions<JwtOptions> jwtOptions)
	{
		httpContext.Response.Cookies.Append(
			key: Name,
			value: refreshToken,
			options: new CookieOptions
			{
				HttpOnly = true,
				Secure = true,
				SameSite = SameSiteMode.Strict,
				Path = "/",
				MaxAge = TimeSpan.FromDays(value: jwtOptions.Value.RefreshTokenTtlDays),
				IsEssential = true
			}
		);
	}

	public static string? Read(HttpContext httpContext)
		=> httpContext.Request.Cookies.TryGetValue(key: Name, value: out string? token) ? token : null;

	public static void Delete(HttpContext httpContext) => httpContext.Response.Cookies.Delete(key: Name, options: new CookieOptions
	{
		HttpOnly = true,
		Secure = true,
		SameSite = SameSiteMode.Strict,
		Path = "/"
	});
}
