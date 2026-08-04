using FinanceTracker.Api.Http.Middleware;
using Microsoft.AspNetCore.Http;

namespace FinanceTracker.Tests.Unit.Api.Infrastructure;

public sealed class SecurityHeadersMiddlewareTests
{
	private static async Task<DefaultHttpContext> InvokeAsync(string path = "/api/v1/accounts")
	{
		DefaultHttpContext context = new DefaultHttpContext
		{
			Request = { Path = path }
		};
		SecurityHeadersMiddleware middleware = new SecurityHeadersMiddleware(next: _ => Task.CompletedTask);

		await middleware.InvokeAsync(context: context);

		return context;
	}

	[Test]
	public async Task InvokeAsync_ShouldSetXContentTypeOptionsToNosniff()
	{
		DefaultHttpContext context = await InvokeAsync();

		await Assert.That(value: context.Response.Headers.XContentTypeOptions.ToString()).IsEqualTo(expected: "nosniff");
	}

	[Test]
	public async Task InvokeAsync_ShouldSetXFrameOptionsToDeny()
	{
		DefaultHttpContext context = await InvokeAsync();

		await Assert.That(value: context.Response.Headers.XFrameOptions.ToString()).IsEqualTo(expected: "DENY");
	}

	[Test]
	public async Task InvokeAsync_ShouldSetReferrerPolicyToNoReferrer()
	{
		DefaultHttpContext context = await InvokeAsync();

		await Assert.That(value: context.Response.Headers["Referrer-Policy"].ToString()).IsEqualTo(expected: "no-referrer");
	}

	[Test]
	public async Task InvokeAsync_ShouldSetCrossOriginOpenerPolicyToSameOrigin()
	{
		DefaultHttpContext context = await InvokeAsync();

		await Assert.That(value: context.Response.Headers["Cross-Origin-Opener-Policy"].ToString()).IsEqualTo(expected: "same-origin");
	}

	[Test]
	public async Task InvokeAsync_ShouldAlwaysCallNext()
	{
		bool nextCalled = false;
		DefaultHttpContext context = new DefaultHttpContext();
		SecurityHeadersMiddleware middleware = new SecurityHeadersMiddleware(next: _ =>
		{
			nextCalled = true;
			return Task.CompletedTask;
		});

		await middleware.InvokeAsync(context: context);

		await Assert.That(value: nextCalled).IsTrue();
	}

	[Test]
	public async Task InvokeAsync_ForAnApiRequest_ShouldForbidLoadingAnything()
	{
		DefaultHttpContext context = await InvokeAsync(path: "/api/v1/accounts");

		await Assert.That(value: context.Response.Headers.ContentSecurityPolicy.ToString())
			.IsEquivalentTo(expected: "default-src 'none'; frame-ancestors 'none'; base-uri 'none'");
	}

	[Test]
	public async Task InvokeAsync_ForTheDocumentationPage_ShouldAllowWhatScalarNeeds()
	{
		DefaultHttpContext context = await InvokeAsync(path: "/scalar/v1");

		string policy = context.Response.Headers.ContentSecurityPolicy.ToString();

		await Assert.That(value: policy).Contains(expected: "cdn.jsdelivr.net").Because(message: """
		   Scalar loads its bundle from a CDN, so the strict policy would leave a blank page. Widening it
		   for this path is the whole reason the policy is not global.
		""");
		await Assert.That(value: policy).DoesNotContain(expected: "default-src 'none'");
	}
}
