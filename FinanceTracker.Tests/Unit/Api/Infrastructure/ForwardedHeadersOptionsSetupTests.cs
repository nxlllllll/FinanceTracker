using System.Net;
using FinanceTracker.Api.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using IPNetwork = System.Net.IPNetwork;

namespace FinanceTracker.Tests.Unit.Api.Infrastructure;

public sealed class ForwardedHeadersOptionsSetupTests
{
	private static ForwardedHeadersOptions Configure(ProxyOptions proxy)
	{
		ForwardedHeadersOptions options = new ForwardedHeadersOptions();
		new ForwardedHeadersOptionsSetup(proxyOptions: Options.Create(options: proxy)).Configure(options: options);
		return options;
	}

	[Test]
	public async Task Configure_ShouldSetXForwardedForAndProtoFlags()
	{
		ForwardedHeadersOptions options = Configure(proxy: new ProxyOptions());

		await Assert.That(value: options.ForwardedHeaders.HasFlag(flag: ForwardedHeaders.XForwardedFor)).IsTrue();
		await Assert.That(value: options.ForwardedHeaders.HasFlag(flag: ForwardedHeaders.XForwardedProto)).IsTrue();
	}

	[Test]
	public async Task Configure_WithKnownProxies_ShouldAddParsedAddresses()
	{
		ForwardedHeadersOptions options = Configure(proxy: new ProxyOptions { KnownProxies = ["10.0.0.5"] });

		await Assert.That(value: options.KnownProxies).Contains(expected: IPAddress.Parse(ipString: "10.0.0.5"));
	}

	[Test]
	public async Task Configure_WithInvalidProxyAddress_ShouldSkipItSilently()
	{
		ForwardedHeadersOptions before = new ForwardedHeadersOptions();
		int defaultCount = before.KnownProxies.Count;

		ForwardedHeadersOptions options = Configure(proxy: new ProxyOptions { KnownProxies = ["not-an-ip"] });

		await Assert.That(value: options.KnownProxies).Count().IsEqualTo(expected: defaultCount);
	}

	[Test]
	public async Task Configure_WithKnownNetworks_ShouldAddParsedNetworks()
	{
		ForwardedHeadersOptions options = Configure(proxy: new ProxyOptions { KnownNetworks = ["172.16.0.0/12"] });

		await Assert.That(value: options.KnownIPNetworks).Contains(expected: IPNetwork.Parse(s: "172.16.0.0/12"));
	}

	[Test]
	public async Task Configure_WithInvalidNetworkEntry_ShouldSkipItSilently()
	{
		ForwardedHeadersOptions before = new ForwardedHeadersOptions();
		int defaultCount = before.KnownIPNetworks.Count;

		ForwardedHeadersOptions options = Configure(proxy: new ProxyOptions { KnownNetworks = ["not-a-network"] });

		await Assert.That(value: options.KnownIPNetworks).Count().IsEqualTo(expected: defaultCount);
	}
}
