using System.Net;
using FinanceTracker.Api.Http;
using Microsoft.AspNetCore.Http;

namespace FinanceTracker.Tests.Unit.Api.Infrastructure;

public sealed class HttpContextExtensionsTests
{
	private static HttpContext ContextWith(string? idempotencyKey = null, IPAddress? remoteAddress = null)
	{
		DefaultHttpContext context = new DefaultHttpContext();

		if (idempotencyKey is not null)
			context.Request.Headers["Idempotency-Key"] = idempotencyKey;

		context.Connection.RemoteIpAddress = remoteAddress;

		return context;
	}

	[Test]
	public async Task AWellFormedIdempotencyKeyIsRead()
	{
		Guid key = Guid.CreateVersion7();

		await Assert.That(value: ContextWith(idempotencyKey: key.ToString()).GetIdempotencyKey()).IsEqualTo(expected: key);
	}

	[Test]
	public async Task AMissingIdempotencyKeyReadsAsNothing()
		=> await Assert.That(value: ContextWith().GetIdempotencyKey()).IsNull();

	[Test]
	public async Task AnIdempotencyKeyThatIsNotAGuidReadsAsNothing()
	{
		await Assert.That(value: ContextWith(idempotencyKey: "not-a-guid").GetIdempotencyKey()).IsNull()
			.Because(message: "the endpoint answers 400 for both cases, and a malformed key is no more usable than an absent one");
	}

	[Test]
	public async Task TheClientAddressIsRead()
	{
		IPAddress address = IPAddress.Parse(ipString: "203.0.113.7");

		await Assert.That(value: ContextWith(remoteAddress: address).GetClientIpAddress()).IsEqualTo(expected: address);
	}

	[Test]
	public async Task AConnectionWithNoPeerFallsBackToNone()
	{
		await Assert.That(value: ContextWith(remoteAddress: null).GetClientIpAddress()).IsEqualTo(expected: IPAddress.None)
			.Because(message: "a null here would reach the rate-limit key builder, and IPAddress.None keeps those callers in one bucket instead of crashing");
	}
}
