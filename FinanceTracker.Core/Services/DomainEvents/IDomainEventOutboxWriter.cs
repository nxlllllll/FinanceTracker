using FinanceTracker.Core.Domains.Abstractions.DomainEvent;

namespace FinanceTracker.Core.Services.DomainEvents;

public interface IDomainEventOutboxWriter
{
	Task WriteAsync(
		IHasDomainEvents entity,
		Guid correlationId,
		CancellationToken ct = default
	);
}