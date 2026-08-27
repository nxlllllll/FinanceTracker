using FinanceTracker.Api.Http.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace FinanceTracker.Tests.Unit.Api.Infrastructure;

public sealed class SecurityHeadersMiddlewareTests
{
	private sealed class RecordingResponseFeature : IHttpResponseFeature
	{
		private readonly List<(Func<object, Task> Callback, object State)> _onStarting = [];

		public int StatusCode { get; set; } = 200;
		public string? ReasonPhrase { get; set; }
		public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
		public Stream Body { get; set; } = Stream.Null;
		public bool HasStarted { get; private set; }

		public void OnStarting(Func<object, Task> callback, object state) => _onStarting.Add(item: (callback, state));

		public void OnCompleted(Func<object, Task> callback, object state) { }

		public async Task StartAsync()
		{
			HasStarted = true;

			foreach ((Func<object, Task> callback, object state) in _onStarting)
				await callback(arg: state);
		}
	}

	private static (DefaultHttpContext Context, RecordingResponseFeature Response) Build(string path)
	{
		RecordingResponseFeature response = new RecordingResponseFeature();

		DefaultHttpContext context = new DefaultHttpContext();
		context.Features.Set<IHttpResponseFeature>(instance: response);
		context.Request.Path = path;

		return (context, response);
	}

	private static async Task<DefaultHttpContext> InvokeAsync(
		string path = "/api/v1/accounts",
		RequestDelegate? next = null)
	{
		(DefaultHttpContext context, RecordingResponseFeature response) = Build(path: path);

		SecurityHeadersMiddleware middleware = new SecurityHeadersMiddleware(next: next ?? (_ => Task.CompletedTask));

		await middleware.InvokeAsync(context: context);
		await response.StartAsync();

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

		await InvokeAsync(next: _ =>
		{
			nextCalled = true;
			return Task.CompletedTask;
		});

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

	[Test]
	public async Task InvokeAsync_ShouldNotSetAnythingBeforeTheResponseStarts()
	{
		(DefaultHttpContext context, RecordingResponseFeature _) = Build(path: "/api/v1/accounts");

		SecurityHeadersMiddleware middleware = new SecurityHeadersMiddleware(next: _ => Task.CompletedTask);

		await middleware.InvokeAsync(context: context);

		await Assert.That(value: context.Response.Headers.ContentSecurityPolicy.ToString()).IsEmpty().Because(message: """
			The headers have to be written at response start, not on the way in. Anything set earlier is
			erased by ExceptionHandlerMiddleware, which calls Response.Clear() before handing a failure to
			its handler — leaving exactly the 500s without a policy. This is the assertion that fails if
			someone moves the assignment back into InvokeAsync for simplicity.
		""");
	}
}
