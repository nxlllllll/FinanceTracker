using FinanceTracker.Api.Http.Middleware;
using Microsoft.AspNetCore.Http;

namespace FinanceTracker.Tests.Unit.Api.Infrastructure;

public sealed class SecurityHeadersMiddlewareTests
{
	private static async Task<DefaultHttpContext> InvokeAsync()
	{
		DefaultHttpContext context = new DefaultHttpContext();
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
	public async Task InvokeAsync_ShouldNotSetContentSecurityPolicy()
	{
		DefaultHttpContext context = await InvokeAsync();

		await Assert.That(value: context.Response.Headers.ContainsKey(key: "Content-Security-Policy")).IsFalse();
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
}
