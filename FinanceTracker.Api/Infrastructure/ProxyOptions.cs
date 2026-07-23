namespace FinanceTracker.Api.Infrastructure;

/// <summary>
/// Configures which reverse proxies/load balancers this API trusts. By default, both lists
/// are empty, meaning redirected headers are completely ignored. When speed is limited
/// and audit logging is performed, the IP address of the load balancer will be displayed.
/// When the reverse proxy server/load balancer is installed, you will need to install
/// <see cref="KnownProxies"/> or <see cref="KnownNetworks"/>
/// </summary>
public sealed class ProxyOptions
{
	/// <summary>Exact IP addresses of trusted proxies as strings.</summary>
	public string[] KnownProxies { get; init; } = [];

	/// <summary>Trusted proxy IP ranges in "address/prefixLength" CIDR notation</summary>
	public string[] KnownNetworks { get; init; } = [];
}
