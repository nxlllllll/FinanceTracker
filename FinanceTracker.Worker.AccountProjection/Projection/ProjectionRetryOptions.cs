using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Worker.AccountProjection.Projection;

public sealed class ProjectionRetryOptions
{
	public const string SectionName = "ProjectionRetry";

	[Required] public int MaxRetries { get; init; }
	[Required] public int BaseDelayMs { get; init; }
	[Required] public bool UseJitter { get; init; }
}