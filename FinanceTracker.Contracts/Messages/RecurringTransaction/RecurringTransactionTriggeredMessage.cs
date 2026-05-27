using FinanceTracker.Core.Domains.Abstractions.Aggregate;

namespace FinanceTracker.Contracts.Messages.RecurringTransaction;

[RoutingKey(routingKey: AggregateTypeNames.RecurringTransaction)]
public sealed record RecurringTransactionTriggeredMessage(
	Guid MessageId,
	Guid RecurringTransactionId,
	Guid AccountId,
	Guid UserId,
	Guid CategoryId,
	decimal Amount,
	string Currency,
	string Direction,
	string? Description,
	DateTimeOffset OccurredAt,
	Guid CorrelationId
) : IRoutableMessage
{
	public string RoutingKey => AggregateTypeNames.RecurringTransaction;
}