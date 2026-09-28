using System.Text.Json;
using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Contracts.Events.Budget;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Observability.Correlation;
using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Infrastructure.Database.Repositories.Budget;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class TestBudgetThresholdAlerts
{
	public static BudgetThresholdAlerts Create(FinanceTrackerContext context)
	{
		IOptionsMonitor<BudgetAlertOptions> options = Substitute.For<IOptionsMonitor<BudgetAlertOptions>>();
		options.CurrentValue.Returns(returnThis: new BudgetAlertOptions { Thresholds = BudgetAlertOptions.DefaultThresholds });

		return new BudgetThresholdAlerts(
			context: context,
			budgetAlertOptions: options,
			integrationEventTypeResolver: new IntegrationEventTypeResolver(
				contractsAssembly: typeof(IIntegrationEvent).Assembly,
				logger: Substitute.For<ILogger<IntegrationEventTypeResolver>>()
			),
			correlationContext: Substitute.For<ICorrelationContext>(),
			dateProvider: FakeDateProvider.Default
		);
	}

	public static async Task<List<BudgetThresholdReachedEvent>> ReadStagedAsync(FinanceTrackerContext context, Guid budgetId)
	{
		await context.SaveChangesAsync();

		List<string> payloads = await context.OutboxMessages.AsNoTracking()
			.Where(predicate: m => m.AggregateType == AggregateTypeNames.Budget && m.AggregateId == budgetId)
			.OrderBy(keySelector: m => m.Id)
			.Select(selector: m => m.Payload)
			.ToListAsync();

		return [..payloads
			.SelectMany(selector: payload => JsonSerializer.Deserialize<OutboxPayload>(json: payload, options: FinanceTrackerJsonOptions.Payload)!.Events)
			.Select(selector: envelope => JsonSerializer.Deserialize<BudgetThresholdReachedEvent>(json: envelope.EventPayload, options: FinanceTrackerJsonOptions.Payload)!)
		];
	}
}
