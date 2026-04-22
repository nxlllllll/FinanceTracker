using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Transaction;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transaction;

public sealed class TransactionRepository(
	IEventStore eventStore
) : ITransactionRepository
{
	private const string AggregateType = nameof(Core.Domains.Transaction.Transaction);
	
	public async Task<Core.Domains.Transaction.Transaction?> GetByIdAsync(
		Guid transactionId,
		CancellationToken ct = default)
	{
		IReadOnlyList<IEvent> events = await eventStore.LoadAsync(aggregateId: transactionId, ct: ct);
		
		if (events.Count == 0)
			return null;
		
		return Core.Domains.Transaction.Transaction.ReconstituteFromHistory(history: events);
	}

	public async Task SaveAsync(
		Core.Domains.Transaction.Transaction transaction,
		CancellationToken ct = default)
	{
		await eventStore.SaveAsync(
			aggregateId: transaction.Id,
			aggregateType: AggregateType,
			events: transaction.Events,
			expectedVersion: transaction.Version - transaction.Events.Count,
			ct: ct
		);
		
		transaction.ClearEvents();
	}
}