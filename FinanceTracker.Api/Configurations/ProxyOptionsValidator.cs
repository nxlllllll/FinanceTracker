using System.Net;
using Microsoft.Extensions.Options;
using IPNetwork = System.Net.IPNetwork;

namespace FinanceTracker.Api.Configurations;

/// <summary>
/// Refuses to start on a proxy configuration that would quietly disable per-IP rate limiting.
/// </summary>
public sealed class ProxyOptionsValidator : IValidateOptions<ProxyOptions>
{
	public ValidateOptionsResult Validate(string? name, ProxyOptions options)
	{
		List<string> failures = [];

		bool hasEntries = options.KnownProxies.Length > 0 || options.KnownNetworks.Length > 0;

		if (!hasEntries && !options.TrustNoProxy)
		{
			failures.Add(item: """
			Proxy trust is unset. If a reverse proxy fronts this API, list it under Proxy:KnownProxies or
			Proxy:KnownNetworks — otherwise X-Forwarded-For is ignored and every client shares the proxy's
			address, so one user's failed logins exhaust the limit for everyone. If nothing fronts the API,
			say so with Proxy:TrustNoProxy = true.
			""");
		}

		if (hasEntries && options.TrustNoProxy)
		{
			failures.Add(item: """
			Proxy:TrustNoProxy is set while proxies are also listed. One of the two is stale, and guessing
			which would mean guessing whether forwarded headers can be believed.
			""");
		}

		failures.AddRange(collection: options.KnownProxies.Where(entry => !IPAddress.TryParse(ipString: entry, address: out _)).Select(entry => $"""
		Proxy:KnownProxies contains '{entry}', which is not an IP address. An unparseable entry is
		skipped rather than trusted, so the proxy behind it would be treated as an ordinary client.
		"""));

		foreach (string entry in options.KnownNetworks)
		{
			if (!IPNetwork.TryParse(s: entry, result: out IPNetwork network))
			{
				failures.Add(item: $"""
				Proxy:KnownNetworks contains '{entry}', which is not CIDR notation such as 10.0.0.0/8. An
				unparseable entry is skipped rather than trusted, so the proxies behind it would be treated
				as ordinary clients.
				""");
				continue;
			}

			if (network.PrefixLength == 0)
			{
				failures.Add(item: $"""
				Proxy:KnownNetworks contains '{entry}', which trusts every address. Any caller could then set
				X-Forwarded-For freely and take a new rate-limit bucket on each request, which is the same as
				having no per-IP limit at all.
				""");
			}
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures: failures)
			: ValidateOptionsResult.Success;
	}
}
