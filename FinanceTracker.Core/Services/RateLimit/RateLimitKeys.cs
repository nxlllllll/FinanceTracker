using System.Net;
using System.Net.Sockets;

namespace FinanceTracker.Core.Services.RateLimit;

public static class RateLimitKeys
{
	private const string GlobalIpPrefix = "ratelimit:ip:";
	private const string AuthIpPrefix = "ratelimit:auth:ip:";
	private const string AuthEmailPrefix = "ratelimit:email:";
	private const string UserPrefix = "ratelimit:user:";

	public static string GetGlobalIp(IPAddress address) => GlobalIpPrefix + GetPartition(address: address);

	public static string GetAuthIp(IPAddress address) => AuthIpPrefix + GetPartition(address: address);

	public static string GetAuthEmail(string email) => AuthEmailPrefix + email;

	public static string GetUser(Guid userId) => UserPrefix + userId;

	public static string GetPartition(IPAddress address)
	{
		if (address.IsIPv4MappedToIPv6)
			address = address.MapToIPv4();

		if (address.AddressFamily != AddressFamily.InterNetworkV6)
			return address.ToString();

		byte[] prefix = address.GetAddressBytes();
		Array.Clear(array: prefix, index: 8, length: 8);

		return new IPAddress(address: prefix) + "/64";
	}
}
