namespace FinanceTracker.Api.Configurations;

/// <summary>
/// Declares which reverse proxies this API trusts to set <c>X-Forwarded-For</c>.
/// </summary>
public sealed class ProxyOptions
{
	public const string SectionName = "Proxy";

	/// <summary>Exact IP addresses of trusted proxies.</summary>
	public string[] KnownProxies { get; init; } = [];

	/// <summary>Trusted proxy ranges in <c>address/prefixLength</c> CIDR notation.</summary>
	public string[] KnownNetworks { get; init; } = [];

	/// <summary>
	/// Set when the API is reached directly and no proxy should be trusted. Client addresses then
	/// come from the connection itself, and forwarded headers are ignored.
	/// </summary>
	public bool TrustNoProxy { get; init; }
}

