using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;

namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class UnresolvableEventEntity
{
	public Guid Id { get; init; }
	public UnresolvableEventType Type { get; init; }
	public Guid ReferenceId { get; init; }
	public string Reason { get; init; } = string.Empty;
	public string Payload { get; init; } = string.Empty;
	public DateTime OccurredAt { get; init; }
}