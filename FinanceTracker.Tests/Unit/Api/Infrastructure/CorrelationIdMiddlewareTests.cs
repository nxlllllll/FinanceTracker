using FinanceTracker.Api.Infrastructure;
using FinanceTracker.Core.Services.Correlation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Api.Infrastructure;

public sealed class CorrelationIdMiddlewareTests
{
	private sealed class RecordingHttpResponseFeature : HttpResponseFeature
	{
		private Func<object, Task>? _callback;
		private object? _state;

		public override void OnStarting(Func<object, Task> callback, object state)
		{
			_callback = callback;
			_state = state;
		}

		public Task FireOnStartingAsync() => _callback is not null ? _callback(arg: _state!) : Task.CompletedTask;
	}

	private ICorrelationContext _correlationContext = null!;
	private CorrelationIdMiddleware _middleware = null!;
	private bool _nextCalled;

	[Before(hookType: Test)]
	public void Setup()
	{
		_correlationContext = Substitute.For<ICorrelationContext>();
		_nextCalled = false;

		_middleware = new CorrelationIdMiddleware(next: _ =>
		{
			_nextCalled = true;
			return Task.CompletedTask;
		});
	}

	private static DefaultHttpContext BuildContext(string? incomingHeader = null)
	{
		FeatureCollection features = new FeatureCollection();
		features.Set<IHttpRequestFeature>(instance: new HttpRequestFeature());
		features.Set<IHttpResponseFeature>(instance: new HttpResponseFeature());
		features.Set<IHttpResponseBodyFeature>(instance: new StreamResponseBodyFeature(stream: Stream.Null));

		DefaultHttpContext context = new DefaultHttpContext(features: features);

		if (incomingHeader is not null)
			context.Request.Headers[CorrelationIdMiddleware.HeaderName] = incomingHeader;

		return context;
	}

	private static (DefaultHttpContext Context, RecordingHttpResponseFeature ResponseFeature) BuildContextWithRecordingFeature(string? incomingHeader = null)
	{
		RecordingHttpResponseFeature responseFeature = new RecordingHttpResponseFeature();

		FeatureCollection features = new FeatureCollection();
		features.Set<IHttpRequestFeature>(instance: new HttpRequestFeature());
		features.Set<IHttpResponseFeature>(instance: responseFeature);
		features.Set<IHttpResponseBodyFeature>(instance: new StreamResponseBodyFeature(stream: Stream.Null));

		DefaultHttpContext context = new DefaultHttpContext(features: features);

		if (incomingHeader is not null)
			context.Request.Headers[CorrelationIdMiddleware.HeaderName] = incomingHeader;

		return (context, responseFeature);
	}

	[Test]
	public async Task InvokeAsync_WithNoIncomingHeader_ShouldGenerateAndSetANewCorrelationId()
	{
		DefaultHttpContext context = BuildContext();

		await _middleware.InvokeAsync(context: context, correlationContext: _correlationContext);

		_correlationContext.Received(requiredNumberOfCalls: 1).Set(correlationId: Arg.Is<Guid>(x => x != Guid.Empty));
	}

	[Test]
	public async Task InvokeAsync_WithValidIncomingHeader_ShouldHonorIt()
	{
		Guid incoming = Guid.CreateVersion7();
		DefaultHttpContext context = BuildContext(incomingHeader: incoming.ToString());

		await _middleware.InvokeAsync(context: context, correlationContext: _correlationContext);

		_correlationContext.Received(requiredNumberOfCalls: 1).Set(correlationId: incoming);
	}

	[Test]
	public async Task InvokeAsync_WithInvalidIncomingHeader_ShouldGenerateANewOne()
	{
		DefaultHttpContext context = BuildContext(incomingHeader: "not-a-guid");

		await _middleware.InvokeAsync(context: context, correlationContext: _correlationContext);

		_correlationContext.Received(requiredNumberOfCalls: 1).Set(correlationId: Arg.Is<Guid>(x => x != Guid.Empty));
	}

	[Test]
	public async Task InvokeAsync_WithEmptyGuidHeader_ShouldGenerateANewOne()
	{
		DefaultHttpContext context = BuildContext(incomingHeader: Guid.Empty.ToString());

		await _middleware.InvokeAsync(context: context, correlationContext: _correlationContext);

		_correlationContext.Received(requiredNumberOfCalls: 1).Set(correlationId: Arg.Is<Guid>(x => x != Guid.Empty));
	}

	[Test]
	public async Task InvokeAsync_ShouldAlwaysCallNext()
	{
		DefaultHttpContext context = BuildContext();

		await _middleware.InvokeAsync(context: context, correlationContext: _correlationContext);

		await Assert.That(value: _nextCalled).IsTrue();
	}

	[Test]
	public async Task InvokeAsync_ShouldEchoTheResolvedCorrelationIdAsAResponseHeader_WhenTheResponseStarts()
	{
		Guid incoming = Guid.CreateVersion7();
		(DefaultHttpContext context, RecordingHttpResponseFeature responseFeature) = BuildContextWithRecordingFeature(incomingHeader: incoming.ToString());

		await _middleware.InvokeAsync(context: context, correlationContext: _correlationContext);

		await responseFeature.FireOnStartingAsync();

		await Assert.That(value: context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString()).IsEqualTo(expected: incoming.ToString());
	}

	[Test]
	public async Task InvokeAsync_WhenResponseNeverStarts_ShouldNotThrow()
	{
		DefaultHttpContext context = BuildContext();

		await Assert.That(action: async () => await _middleware.InvokeAsync(
			context: context,
			correlationContext: _correlationContext
		)).ThrowsNothing();
	}
}
