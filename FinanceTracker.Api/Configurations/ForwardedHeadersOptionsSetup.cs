using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using IPNetwork = System.Net.IPNetwork;

namespace FinanceTracker.Api.Configurations;

/// <summary>
/// Configures <c>X-Forwarded-For</c>/<c>X-Forwarded-Proto</c> trust from <see cref="ProxyOptions"/>.
/// The header is a no-op unless a deployment explicitly configures who's allowed to set it.
/// </summary>
public sealed class ForwardedHeadersOptionsSetup(
	IOptions<ProxyOptions> proxyOptions
) : IConfigureOptions<ForwardedHeadersOptions>
{
	public void Configure(ForwardedHeadersOptions options)
	{
		ProxyOptions proxy = proxyOptions.Value;

		options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

		foreach (string entry in proxy.KnownProxies)
			if (IPAddress.TryParse(ipString: entry, address: out IPAddress? address))
				options.KnownProxies.Add(item: address);

		foreach (string entry in proxy.KnownNetworks)
			if (IPNetwork.TryParse(s: entry, result: out IPNetwork network))
				options.KnownIPNetworks.Add(item: network);
	}
}
