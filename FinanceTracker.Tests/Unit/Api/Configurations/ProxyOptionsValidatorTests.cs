using FinanceTracker.Api.Configurations;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Tests.Unit.Api.Configurations;

public sealed class ProxyOptionsValidatorTests
{
	private readonly ProxyOptionsValidator _validator = new ProxyOptionsValidator();

	private ValidateOptionsResult Validate(ProxyOptions options)
		=> _validator.Validate(name: null, options: options);

	[Test]
	public async Task DeclaringNoProxyIsAccepted()
	{
		ValidateOptionsResult result = Validate(options: new ProxyOptions { TrustNoProxy = true });

		await Assert.That(value: result.Succeeded).IsTrue();
	}

	[Test]
	public async Task ListingATrustedProxyIsAccepted()
	{
		ValidateOptionsResult result = Validate(options: new ProxyOptions
		{
			KnownProxies = ["10.0.0.5"],
			KnownNetworks = ["10.0.0.0/8"]
		});

		await Assert.That(value: result.Succeeded).IsTrue();
	}

	[Test]
	public async Task SayingNothingAboutProxiesIsRefused()
	{
		ValidateOptionsResult result = Validate(options: new ProxyOptions());

		await Assert.That(value: result.Failed).IsTrue()
			.Because(message: "silence here is indistinguishable from a misconfiguration, so it cannot be treated as a choice");
	}

	[Test]
	public async Task ClaimingNoProxyWhileListingOneIsRefused()
	{
		ValidateOptionsResult result = Validate(options: new ProxyOptions
		{
			KnownProxies = ["10.0.0.5"],
			TrustNoProxy = true
		});

		await Assert.That(value: result.Failed).IsTrue()
			.Because(message: "one of the two settings is stale and guessing which means guessing whether forwarded headers can be believed");
	}

	[Test]
	public async Task AnAddressThatDoesNotParseIsRefused()
	{
		ValidateOptionsResult result = Validate(options: new ProxyOptions { KnownProxies = ["10.0.0.5", "not-an-address"] });

		await Assert.That(value: result.Failed).IsTrue();
		await Assert.That(value: result.Failures?.Count()).IsEqualTo(expected: 1)
			.Because(message: "only the malformed entry should be reported, not the valid one beside it");
	}

	[Test]
	public async Task ANetworkThatDoesNotParseIsRefused()
	{
		ValidateOptionsResult result = Validate(options: new ProxyOptions { KnownNetworks = ["10.0.0.0"] });

		await Assert.That(value: result.Failed).IsTrue()
			.Because(message: "an entry without a prefix length is skipped at runtime rather than trusted, so the proxy behind it would count as a client");
	}

	[Test]
	public async Task ANetworkCoveringEverythingIsRefused()
	{
		ValidateOptionsResult result = Validate(options: new ProxyOptions { KnownNetworks = ["0.0.0.0/0"] });

		await Assert.That(value: result.Failed).IsTrue();
	}

	[Test]
	public async Task AnIPv6NetworkCoveringEverythingIsAlsoRefused()
	{
		ValidateOptionsResult result = Validate(options: new ProxyOptions { KnownNetworks = ["::/0"] });

		await Assert.That(value: result.Failed).IsTrue()
			.Because(message: "the IPv6 form of trusting everything is no safer than the IPv4 one");
	}

	[Test]
	public async Task EveryProblemIsReportedTogether()
	{
		ValidateOptionsResult result = Validate(options: new ProxyOptions
		{
			KnownProxies = ["nonsense", "also-nonsense"],
			KnownNetworks = ["0.0.0.0/0", "garbage"],
			TrustNoProxy = true
		});

		await Assert.That(value: result.Failures?.Count()).IsEqualTo(expected: 5)
			.Because(message: "collecting failures means one restart fixes everything instead of one per typo");
	}
}
