using FinanceTracker.Contracts.Events.Abstraction;

namespace FinanceTracker.Contracts.Events.Budget;

[IntegrationEventType(name: "budget.threshold_reached")]
public sealed record BudgetThresholdReachedEvent(
	Guid EventId,
	Guid BudgetId,
	Guid UserId,
	Guid CategoryId,
	int Threshold,
	decimal Spent,
	decimal Limit,
	string Currency,
	int Version,
	DateTimeOffset OccurredAt
) : IUserNotification
{
	Guid IIntegrationEvent.AggregateId => BudgetId;
}
