using FinanceTracker.Core.Domains.Operation;

namespace FinanceTracker.Infrastructure.Database.Context.Operation;

public sealed class OperationEntity
{
	public Guid Id { get; init; }
	public Guid UserId { get; init; }
	public OperationType Type { get; init; }
	public DateTimeOffset OccurredAt { get; init; }
	public string? Description { get; set; }
	public string Payload { get; set; } = null!;
}
