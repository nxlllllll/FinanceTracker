using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Infrastructure.Configurations.Options;

/// <summary>Configuration for Redis connection. Bind from <c>appsettings.json</c> under the <c>"Redis"</c> section.</summary>
public sealed class RedisOptions
{
	public const string SectionName = "Redis";

	/// <summary>Redis connection string (e.g. <c>localhost:6379</c> or a full StackExchange.Redis connection string).</summary>
	[Required]
	[MinLength(1)]
	public string ConnectionString { get; init; } = "localhost:6379";

	/// <summary>
	/// Key prefix applied to all cache entries to namespace them within a shared Redis instance.
	/// Default: <c>ft:</c>.
	/// </summary>
	[Required]
	public string InstanceName { get; init; } = "ft:";
}