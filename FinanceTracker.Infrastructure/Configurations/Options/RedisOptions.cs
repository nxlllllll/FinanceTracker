using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Infrastructure.Configurations.Options;

public sealed class RedisOptions
{
	public const string SectionName = "Redis";

	[Required]
	[MinLength(1)]
	public string ConnectionString { get; init; } = "localhost:6379";

	[Required]
	public string InstanceName { get; init; } = "ft:";
}
