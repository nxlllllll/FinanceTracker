using FinanceTracker.Api.Http;
using FinanceTracker.Infrastructure.Services.Token;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Tests.Unit.Api.Http;

public sealed class RefreshTokenCookieTests
{
	private const string CookieName = "__Host-refresh-token";

	private static IOptions<JwtOptions> JwtOptionsWith(int refreshTokenTtlDays)
		=> Options.Create(options: new JwtOptions { RefreshTokenTtlDays = refreshTokenTtlDays });

	private static string AppendedCookie(int refreshTokenTtlDays = 30, string token = "opaque-token")
	{
		DefaultHttpContext context = new DefaultHttpContext();

		RefreshTokenCookie.Append(
			httpContext: context,
			refreshToken: token,
			jwtOptions: JwtOptionsWith(refreshTokenTtlDays: refreshTokenTtlDays)
		);

		return context.Response.Headers.SetCookie.ToString();
	}

	[Test]
	public async Task TheCookieIsWrittenUnderTheHostPrefixedName()
	{
		await Assert.That(value: AppendedCookie()).Contains(expected: $"{CookieName}=opaque-token");
	}

	[Test]
	public async Task TheCookieIsHiddenFromScript()
	{
		await Assert.That(value: AppendedCookie()).Contains(expected: "httponly")
			.Because(message: "a refresh token readable from JavaScript survives any XSS on the page");
	}

	[Test]
	public async Task TheCookieTravelsOnlyOverTLS()
	{
		await Assert.That(value: AppendedCookie()).Contains(expected: "secure");
	}

	[Test]
	public async Task TheCookieIsNotSentOnCrossSiteRequests()
	{
		await Assert.That(value: AppendedCookie()).Contains(expected: "samesite=strict")
			.Because(message: "refresh and logout accept the cookie without a bearer token, so Strict is what stands in for CSRF protection");
	}

	[Test]
	public async Task TheCookieIsScopedToTheWholeSite()
	{
		await Assert.That(value: AppendedCookie()).Contains(expected: "path=/");
	}

	[Test]
	public async Task TheCookieCarriesNoDomainSoTheHostPrefixStaysValid()
	{
		await Assert.That(value: AppendedCookie()).DoesNotContain(expected: "domain=")
			.Because(message: "a __Host- cookie with a Domain attribute is silently rejected by the browser");
	}

	[Test]
	public async Task TheCookieOutlivesTheAccessTokenByTheConfiguredNumberOfDays()
	{
		string cookie = AppendedCookie(refreshTokenTtlDays: 14);

		await Assert.That(value: cookie).Contains(expected: $"max-age={TimeSpan.FromDays(value: 14).TotalSeconds:F0}");
	}

	[Test]
	public async Task ThePresentedTokenIsReadBack()
	{
		DefaultHttpContext context = new DefaultHttpContext();
		context.Request.Headers.Cookie = $"{CookieName}=opaque-token";

		await Assert.That(value: RefreshTokenCookie.Read(httpContext: context)).IsEqualTo(expected: "opaque-token");
	}

	[Test]
	public async Task NoCookieReadsAsNothingRatherThanEmptiness()
	{
		await Assert.That(value: RefreshTokenCookie.Read(httpContext: new DefaultHttpContext())).IsNull()
			.Because(message: "an empty string would reach the token validator as a credential worth checking");
	}

	[Test]
	public async Task LogoutExpiresTheCookieWithTheSameAttributesItWasSetWith()
	{
		DefaultHttpContext context = new DefaultHttpContext();

		RefreshTokenCookie.Delete(httpContext: context);

		string cookie = context.Response.Headers.SetCookie.ToString();

		await Assert.That(value: cookie).Contains(expected: CookieName);
		await Assert.That(value: cookie).Contains(expected: "expires=Thu, 01 Jan 1970");
		await Assert.That(value: cookie).Contains(expected: "path=/");
		await Assert.That(value: cookie).Contains(expected: "secure");
		await Assert.That(value: cookie).Contains(expected: "samesite=strict");
	}
}
