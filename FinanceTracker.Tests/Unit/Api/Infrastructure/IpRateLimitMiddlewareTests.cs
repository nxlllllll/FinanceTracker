using System.Net;
using System.Net.Mime;
using System.Text.Json;
using FinanceTracker.Api.Configurations;
using FinanceTracker.Api.Http.Middleware;
using FinanceTracker.Core.Observability.Correlation;
using FinanceTracker.Core.Services.RateLimit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Api.Infrastructure;

public sealed class IpRateLimitMiddlewareTests
{
	private IRateLimiter _rateLimiter = null!;
	private ICorrelationContext _correlationContext = null!;
	private IpRateLimitOptions _options = null!;
	private IpRateLimitMiddleware _middleware = null!;
	private IOptionsMonitor<IpRateLimitOptions> _optionsMonitor = null!;
	private bool _nextCalled;

	[Before(hookType: Test)]
	public void Setup()
	{
		_nextCalled = false;
		_options = new IpRateLimitOptions();

		_rateLimiter = Substitute.For<IRateLimiter>();
		_rateLimiter.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RateLimitResult.Allowed());

		_correlationContext = Substitute.For<ICorrelationContext>();
		_correlationContext.CorrelationId.Returns(returnThis: Guid.CreateVersion7());

		_optionsMonitor = Substitute.For<IOptionsMonitor<IpRateLimitOptions>>();
		_optionsMonitor.CurrentValue.Returns(returnThis: _ => _options);

		_middleware = new IpRateLimitMiddleware(
			next: _ =>
			{
				_nextCalled = true;
				return Task.CompletedTask;
			},
			options: _optionsMonitor,
			logger: NullLogger<IpRateLimitMiddleware>.Instance
		);
	}

	private static (DefaultHttpContext Context, MemoryStream Body) BuildContext(string? remoteAddress)
	{
		MemoryStream body = new MemoryStream();

		FeatureCollection features = new FeatureCollection();
		features.Set<IHttpRequestFeature>(instance: new HttpRequestFeature { Path = "/api/v1/accounts" });
		features.Set<IHttpResponseFeature>(instance: new HttpResponseFeature());
		features.Set<IHttpResponseBodyFeature>(instance: new StreamResponseBodyFeature(stream: body));

		DefaultHttpContext context = new DefaultHttpContext(features: features)
		{
			RequestServices = new ServiceCollection().BuildServiceProvider()
		};

		if (remoteAddress is not null)
			context.Connection.RemoteIpAddress = IPAddress.Parse(ipString: remoteAddress);

		return (context, body);
	}

	private async Task InvokeAsync(DefaultHttpContext context) => await _middleware.InvokeAsync(
		context: context,
		rateLimiter: _rateLimiter,
		correlationContext: _correlationContext
	);

	private string? CountedKey()
	{
		object?[]? arguments = _rateLimiter.ReceivedCalls().FirstOrDefault()?.GetArguments();
		return (string?)arguments?[0];
	}

	private async Task<string?> KeyForAsync(string remoteAddress)
	{
		(DefaultHttpContext context, _) = BuildContext(remoteAddress: remoteAddress);
		await InvokeAsync(context: context);
		return CountedKey();
	}

	[Test]
	public async Task IPv4CallersAreCountedIndividually()
	{
		string? key = await KeyForAsync(remoteAddress: "203.0.113.7");

		await Assert.That(value: key).IsEqualTo(expected: "ratelimit:ip:203.0.113.7");
	}

	[Test]
	public async Task AMappedIPv4CallerIsCountedAsThatIPv4Caller()
	{
		string? mapped = await KeyForAsync(remoteAddress: "::ffff:203.0.113.7");

		await Assert.That(value: mapped).IsEqualTo(expected: "ratelimit:ip:203.0.113.7")
			.Because(message: "unwrapping mapped addresses is what keeps IPv4 callers out of the /64 truncation");
	}

	[Test]
	public async Task IPv6CallersSharingASubnetAreCountedTogether()
	{
		string? first = await KeyForAsync(remoteAddress: "2001:db8:1:2:aaaa:bbbb:cccc:dddd");

		_rateLimiter.ClearReceivedCalls();

		string? second = await KeyForAsync(remoteAddress: "2001:db8:1:2::1");

		await Assert.That(value: first).IsEqualTo(expected: second)
			.Because(message: "a residential IPv6 allocation is a whole subnet the client picks freely from");

		await Assert.That(value: first).IsEqualTo(expected: "ratelimit:ip:2001:db8:1:2::/64");
	}

	[Test]
	public async Task IPv6CallersInDifferentSubnetsAreCountedApart()
	{
		string? first = await KeyForAsync(remoteAddress: "2001:db8:1:2::1");

		_rateLimiter.ClearReceivedCalls();

		string? second = await KeyForAsync(remoteAddress: "2001:db8:1:3::1");

		await Assert.That(value: first)!.IsNotEqualTo(notExpected: second);
	}

	[Test]
	public async Task TheConfiguredWindowIsPassedThrough()
	{
		IpRateLimitOptions options = new IpRateLimitOptions { RequestsPerWindow = 42, WindowSeconds = 7 };
		_optionsMonitor.CurrentValue.Returns(returnThis: _ => options);

		(DefaultHttpContext context, _) = BuildContext(remoteAddress: "203.0.113.7");
		await InvokeAsync(context: context);

		await _rateLimiter.Received().IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: 42,
			windowSeconds: 7,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ADisabledLimitNeverReachesRedis()
	{
		IpRateLimitOptions options = new IpRateLimitOptions { Enabled = false };
		_optionsMonitor.CurrentValue.Returns(returnThis: _ => options);

		(DefaultHttpContext context, _) = BuildContext(remoteAddress: "203.0.113.7");
		await InvokeAsync(context: context);

		await Assert.That(value: _rateLimiter.ReceivedCalls()).IsEmpty()
			.Because(message: "turning the limit off must also stop paying for it");

		await Assert.That(value: _nextCalled).IsTrue();
	}

	[Test]
	public async Task ARequestWithNoPeerAddressIsLetThrough()
	{
		(DefaultHttpContext context, _) = BuildContext(remoteAddress: null);
		await InvokeAsync(context: context);

		await Assert.That(value: _rateLimiter.ReceivedCalls()).IsEmpty();

		await Assert.That(value: _nextCalled).IsTrue()
			.Because(message: "unix sockets and in-process hosts have no peer; bucketing them together would deny unrelated callers as a group");
	}

	[Test]
	public async Task AnAdmittedRequestContinuesDownThePipeline()
	{
		(DefaultHttpContext context, _) = BuildContext(remoteAddress: "203.0.113.7");
		await InvokeAsync(context: context);

		await Assert.That(value: _nextCalled).IsTrue();
		await Assert.That(value: context.Response.StatusCode).IsEqualTo(expected: StatusCodes.Status200OK);
	}

	[Test]
	public async Task ARefusedRequestIsAnsweredWithoutReachingThePipeline()
	{
		_rateLimiter.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RateLimitResult.Denied(retryAfterSeconds: 17));

		(DefaultHttpContext context, MemoryStream body) = BuildContext(remoteAddress: "203.0.113.7");
		await InvokeAsync(context: context);

		await Assert.That(value: _nextCalled).IsFalse()
			.Because(message: "the point of refusing before authentication is that the work is never done");

		await Assert.That(value: context.Response.StatusCode).IsEqualTo(expected: StatusCodes.Status429TooManyRequests);
		await Assert.That(value: context.Response.Headers.RetryAfter.ToString()).IsEqualTo(expected: "17");
		await Assert.That(value: context.Response.ContentType).IsEqualTo(expected: MediaTypeNames.Application.ProblemJson);

		using JsonDocument problem = JsonDocument.Parse(utf8Json: body.ToArray());

		await Assert.That(value: problem.RootElement.GetProperty(propertyName: "code").GetString())
			.IsEqualTo(expected: "rate_limit.ip_exceeded")
			.Because(message: "the code is what separates this 429 from the per-user one in logs and dashboards");
	}
}
