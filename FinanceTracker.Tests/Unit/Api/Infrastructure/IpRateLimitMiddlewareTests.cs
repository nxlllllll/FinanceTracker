using System.Net;
using System.Net.Mime;
using System.Text.Json;
using FinanceTracker.Api.Configurations;
using FinanceTracker.Api.Http.Middleware;
using FinanceTracker.Core.Observability.Correlation;
using FinanceTracker.Core.Observability.Metrics;
using FinanceTracker.Core.Services.RateLimit;
using FinanceTracker.Infrastructure.Services.RateLimit;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Api.Infrastructure;

[NotInParallel]
public sealed class IpRateLimitMiddlewareTests
{
	private const int WindowSeconds = 60;
	private const string RefusedInstrument = "ratelimit.refused";

	private IRateLimiter _rateLimiter = null!;
	private ICorrelationContext _correlationContext = null!;
	private IpRateLimitOptions _options = null!;
	private IpRateLimitMiddleware _middleware = null!;
	private IOptionsMonitor<IpRateLimitOptions> _optionsMonitor = null!;
	private CapturingLogger<IpRateLimitMiddleware> _logger = null!;
	private ControllableDateProvider _clock = null!;
	private bool _nextCalled;

	[Before(hookType: Test)]
	public void Setup()
	{
		_nextCalled = false;
		_options = new IpRateLimitOptions();
		_logger = new CapturingLogger<IpRateLimitMiddleware>();
		_clock = new ControllableDateProvider(initial: new DateTimeOffset(
			year: 2024,
			month: 1,
			day: 15,
			hour: 12,
			minute: 0,
			second: 0,
			offset: TimeSpan.Zero
		));

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
			logger: _logger
		);
	}

	private void UseSlidingWindowLimiter()
	{
		_options = new IpRateLimitOptions { RequestsPerWindow = 1, WindowSeconds = WindowSeconds };

		_rateLimiter = new InMemoryRateLimiter(
			dateProvider: _clock,
			options: new FakeOptionsMonitor<InMemoryRateLimiterOptions>(value: new InMemoryRateLimiterOptions())
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

	private async Task SendAsync(string remoteAddress, int requests)
	{
		for (int i = 0; i < requests; i++)
		{
			(DefaultHttpContext context, _) = BuildContext(remoteAddress: remoteAddress);
			await InvokeAsync(context: context);
		}
	}

	[Test]
	public async Task RepeatedRefusalsFromOneAddressAreLoggedOncePerWindow()
	{
		UseSlidingWindowLimiter();

		await SendAsync(remoteAddress: "203.0.113.7", requests: 4);

		await Assert.That(value: _logger.LogCount).IsEqualTo(expected: 1).Because(message: """
			One address hammering the API produces hundreds of refusals a second, and a line for each
			pushes everything else out of the log store. The first line of the window already carries the
			address, the limit and the path; the rest repeat it.
		""");
	}

	[Test]
	public async Task EveryRefusedAddressIsNamedInTheLog()
	{
		UseSlidingWindowLimiter();

		await SendAsync(remoteAddress: "203.0.113.7", requests: 3);
		await SendAsync(remoteAddress: "203.0.113.8", requests: 3);

		await Assert.That(value: _logger.LogCount).IsEqualTo(expected: 2).Because(message: """
			Suppression is held per address rather than globally, because the log is the only place an
			address is recorded at all — the metric carries no address by design.
		""");
	}

	[Test]
	public async Task ARefusedAddressIsLoggedAgainInTheNextWindow()
	{
		UseSlidingWindowLimiter();

		await SendAsync(remoteAddress: "203.0.113.7", requests: 3);

		_clock.Advance(by: TimeSpan.FromSeconds(value: WindowSeconds + 1));

		await SendAsync(remoteAddress: "203.0.113.7", requests: 3);

		await Assert.That(value: _logger.LogCount).IsEqualTo(expected: 2).Because(message: """
			An address still being refused an hour later is news again, not the same event. Suppression
			that outlived its window would make an ongoing flood look like it had stopped.
		""");
	}

	[Test]
	public async Task EveryRefusalIsCounted()
	{
		UseSlidingWindowLimiter();

		using MetricCollector collector = new MetricCollector(RefusedInstrument);

		await SendAsync(remoteAddress: "203.0.113.7", requests: 4);

		await Assert.That(value: collector.Total(
			instrument: RefusedInstrument,
			tags: (FinanceTrackerMetrics.Tags.Limit, FinanceTrackerMetrics.RateLimits.Ip)
		)).IsEqualTo(expected: 3).Because(message: """
			The log reports one line per address per window, so the counter is the only thing that knows
			how large a flood is. The DDoS alert is built on its rate, and a refusal that increments
			nothing is invisible to it.
		""");
	}
}
