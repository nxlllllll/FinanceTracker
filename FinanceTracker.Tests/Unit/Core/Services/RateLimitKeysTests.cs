using System.Net;
using FinanceTracker.Core.Services.RateLimit;

namespace FinanceTracker.Tests.Unit.Core.Services;

public sealed class RateLimitKeysTests
{
	private static readonly IPAddress Ipv4 = IPAddress.Parse(ipString: "203.0.113.7");
	private static readonly IPAddress Ipv6 = IPAddress.Parse(ipString: "2001:db8:1:2:3:4:5:6");
	private static readonly Guid UserId = Guid.Parse(input: "0198b2c0-0000-7000-8000-000000000001");

	private static string[] AllKeys() =>
	[
		RateLimitKeys.GetGlobalIp(address: Ipv4),
		RateLimitKeys.GetAuthIp(address: Ipv4),
		RateLimitKeys.GetAuthEmail(email: "user@example.com"),
		RateLimitKeys.GetUser(userId: UserId)
	];

	[Test]
	public async Task EveryBuilder_ForTheSameSubject_ShouldProduceAKeyNoOtherBuilderCanProduce()
	{
		string[] keys = AllKeys();

		foreach (string key in keys)
		{
			foreach (string other in keys)
			{
				if (ReferenceEquals(objA: key, objB: other))
					continue;

				await Assert.That(value: key.StartsWith(value: other, comparisonType: StringComparison.Ordinal)).IsFalse().Because(message: $"""
					'{key}' and '{other}' share a prefix, so one limiter's counter lands in the other's
					sorted set. Every limiter runs the same Lua script against the key it is given: it trims
					the set by its own window and re-arms the TTL to its own length. Two limiters on one key
					therefore consume each other's budget and truncate each other's window, which reads as a
					limit that fires early and a window that expires late — neither visible from either
					limiter's own tests.
				""");
			}
		}
	}

	[Test]
	public async Task TheGlobalAndAuthIpBuilders_ForOneAddress_ShouldNotAgree()
	{
		string global = RateLimitKeys.GetGlobalIp(address: Ipv4);
		string auth = RateLimitKeys.GetAuthIp(address: Ipv4);

		await Assert.That(value: global).IsNotEqualTo(notExpected: auth).Because(message: """
			The per-request ceiling and the pre-authentication ceiling are counted over different windows
			with different limits. Sharing a bucket lets ordinary traffic exhaust the login budget, and lets
			ordinary traffic trim login attempts out of the brute-force window ahead of time.
		""");
	}

	[Test]
	public async Task AnIpv6Address_ShouldBeCountedAgainstItsSixtyFourBitPrefix()
	{
		IPAddress sameNetwork = IPAddress.Parse(ipString: "2001:db8:1:2:ffff:ffff:ffff:ffff");

		await Assert.That(value: RateLimitKeys.GetPartition(address: Ipv6))
			.IsEqualTo(expected: RateLimitKeys.GetPartition(address: sameNetwork))
			.Because(message: """
				A single IPv6 client is routinely handed a whole /64. Counting the full address lets it take
				a fresh bucket per request, which is the same as having no limit at all.
			""");
	}

	[Test]
	public async Task AnIpv4MappedAddress_ShouldBeCountedAsTheIpv4ItRepresents()
	{
		IPAddress mapped = Ipv4.MapToIPv6();

		await Assert.That(value: RateLimitKeys.GetPartition(address: mapped))
			.IsEqualTo(expected: RateLimitKeys.GetPartition(address: Ipv4))
			.Because(message: """
				Kestrel reports a dual-stack socket's IPv4 peers as ::ffff:a.b.c.d. Left unmapped, the same
				client counts twice — once per representation — and the /64 collapse would put every mapped
				IPv4 address into one shared bucket.
			""");
	}

	[Test]
	public async Task TheAuthEmailAndUserBuilders_ShouldKeepTheirExistingPrefixes()
	{
		await Assert.That(value: RateLimitKeys.GetAuthEmail(email: "user@example.com")).IsEqualTo(expected: "ratelimit:email:user@example.com");
		await Assert.That(value: RateLimitKeys.GetUser(userId: UserId)).IsEqualTo(expected: $"ratelimit:user:{UserId}").Because(message: """
			These two are pinned because a rename silently resets every live counter: the old keys keep
			their TTL while nothing reads them, so limits are effectively off for one window after deploy.
		""");
	}
}
