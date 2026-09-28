using FinanceTracker.Contracts.Events.Budget;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Observability.Correlation;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Budget;
using FinanceTracker.Infrastructure.Database.Context.Outbox;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Database.Repositories.Budget;

public sealed class BudgetThresholdAlerts(
	FinanceTrackerContext context,
	IOptionsMonitor<BudgetAlertOptions> budgetAlertOptions,
	IIntegrationEventTypeResolver integrationEventTypeResolver,
	ICorrelationContext correlationContext,
	IDateProvider dateProvider
)
{
	public void StageIfCrossed(
		BudgetEntity budget,
		decimal spentBefore,
		decimal limitBefore,
		decimal spentAfter,
		int version)
	{
		if (!budget.IsActive)
			return;

		int? threshold = BudgetThresholds.GetHighestCrossed(
			spentBefore: spentBefore,
			limitBefore: limitBefore,
			spentAfter: spentAfter,
			limitAfter: budget.Amount,
			thresholds: budgetAlertOptions.CurrentValue.Thresholds
		);

		if (threshold is null)
			return;

		BudgetThresholdReachedEvent integrationEvent = new BudgetThresholdReachedEvent(
			EventId: Guid.CreateVersion7(),
			BudgetId: budget.Id,
			UserId: budget.UserId,
			CategoryId: budget.CategoryId,
			Threshold: threshold.Value,
			Spent: spentAfter,
			Limit: budget.Amount,
			Currency: budget.Currency,
			Version: version,
			OccurredAt: dateProvider.UtcNow
		);

		context.OutboxMessages.Add(entity: OutboxMessageFactory.CreateMessage(
			aggregateId: budget.Id,
			aggregateType: AggregateTypeNames.Budget,
			correlationId: correlationContext.CorrelationId,
			envelopes: [OutboxMessageFactory.CreateEnvelope(integrationEvent: integrationEvent, integrationEventTypeResolver: integrationEventTypeResolver)],
			now: dateProvider.UtcNow
		));
	}
}
