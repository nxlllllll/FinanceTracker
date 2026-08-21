using System.Text.Json;
using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Observability.Correlation;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels.RecurringTransaction;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Utilities;
using FinanceTracker.Worker.Shared.Job;
using FinanceTracker.Worker.Shared.RabbitMQ.Publisher;
using Microsoft.Extensions.Options;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.RecurringTransaction.Job;

[DisallowConcurrentExecution]
public sealed class RecurringTransactionHandlingJob(
	IRecurringTransactionReadRepository recurringTransactionReadRepository,
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IUnresolvableEventWriteRepository unresolvableEventWriteRepository,
	IUnitOfWork unitOfWork,
	IRabbitMqPublisher publisher,
	ICorrelationContext correlationContext,
	IDateProvider dateProvider,
	IOptionsMonitor<RecurringTransactionJobOptions> options,
	ILogger<RecurringTransactionHandlingJob> logger
) : BaseJob<RecurringTransactionJobOptions>(options: options, logger: logger)
{
	protected override async Task ProcessAsync(RecurringTransactionJobOptions options, CancellationToken ct)
	{
		DateTimeOffset now = dateProvider.UtcNow;

		logger.ZLogInformation(message: $"[{correlationContext.CorrelationId}] Processing due recurring transactions as of {now:G}.");

		await EscalateOverdueTransactionsAsync(now: now, options: options, ct: ct);

		int processed = 0;
		int failed = 0;

		IReadOnlyList<RecurringTransactionReadModel> dueTransactions = await recurringTransactionReadRepository.GetDueAsync(asOf: now, ct: ct);

		foreach (RecurringTransactionReadModel transaction in dueTransactions)
		{
			try
			{
				Guid messageId = DeterministicGuid.Create(
					baseId: transaction.Id,
					occurrence: transaction.NextDueAtUtc
				);

				DateTimeOffset nextDueAtUtc = RecurringDueDate.Next(
					dayOfMonth: transaction.DayOfMonth,
					timeZone: transaction.TimeZone,
					after: transaction.NextDueAtUtc
				);

				await publisher.PublishAsync(message: new RecurringTransactionTriggeredMessage(
					MessageId: messageId,
					RecurringTransactionId: transaction.Id,
					AccountId: transaction.AccountId,
					UserId: transaction.UserId,
					CategoryId: transaction.CategoryId,
					Amount: transaction.Amount.Amount,
					Currency: transaction.Amount.Currency.Value,
					Direction: transaction.Direction.ToString(),
					Description: transaction.Description,
					OccurredAt: transaction.NextDueAtUtc,
					CorrelationId: correlationContext.CorrelationId
				), correlationId: correlationContext.CorrelationId, ct: ct);

				await unitOfWork.ExecuteInTransactionAsync(operation: async () => await recurringTransactionWriteRepository.MarkExecutedAsync(
					recurringTransactionId: transaction.Id,
					executedAt: now,
					nextDueAtUtc: nextDueAtUtc,
					expectedVersion: transaction.RowVersion,
					ct: ct
				), ct: ct);

				logger.ZLogInformation(message:
					$"[{correlationContext.CorrelationId}] Processed recurring transaction {transaction.Id}, due " +
					$"{transaction.NextDueAtUtc:G}, next {nextDueAtUtc:G} ({++processed})."
				);
			}
			catch (Exception ex)
			{
				failed++;
				logger.ZLogError(exception: ex, message:
					$"[{correlationContext.CorrelationId}] Failed to process recurring transaction {transaction.Id}. " +
					$"It stays due and will be retried on the next run."
				);
			}
		}

		if (failed > 0)
			logger.ZLogWarning(message: $"[{correlationContext.CorrelationId}] Completed with {failed} failure(s).");
	}

	private async Task EscalateOverdueTransactionsAsync(
		DateTimeOffset now,
		RecurringTransactionJobOptions options,
		CancellationToken ct)
	{
		DateTimeOffset threshold = now.AddHours(hours: -options.OverdueAfterHours);
		IReadOnlyList<RecurringTransactionReadModel> overdue = await recurringTransactionReadRepository.GetOverdueAsync(before: threshold, ct: ct);

		foreach (RecurringTransactionReadModel transaction in overdue)
		{
			try
			{
				await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
				{
					await unresolvableEventWriteRepository.CreateAsync(
						type: UnresolvableEventType.RecurringTransactionFailed,
						referenceId: transaction.Id,
						reason: $"Due at {transaction.NextDueAtUtc:u} and still not executed {options.OverdueAfterHours}h later; detected on {now:u}.",
						payload: JsonSerializer.Serialize(value: new
						{
							recurringTransactionId = transaction.Id,
							scheduledDayOfMonth = transaction.DayOfMonth,
							timeZone = transaction.TimeZone.Value,
							nextDueAtUtc = transaction.NextDueAtUtc,
							accountId = transaction.AccountId,
							categoryId = transaction.CategoryId,
							detectedOn = now
						}),
						occurredAt: now,
						ct: ct
					);

					await recurringTransactionWriteRepository.MarkMissedAsync(
						recurringTransactionId: transaction.Id,
						missedAt: now,
						expectedVersion: transaction.RowVersion,
						ct: ct
					);
				}, ct: ct);

				logger.ZLogError(message:
					$"[{correlationContext.CorrelationId}] Recurring transaction {transaction.Id} was due " +
					$"{transaction.NextDueAtUtc:u} and is still unexecuted — escalated to unresolvable_events."
				);
			}
			catch (Exception ex)
			{
				logger.ZLogError(exception: ex, message: $"[{correlationContext.CorrelationId}] Failed to escalate overdue recurring transaction {transaction.Id}.");
			}
		}
	}
}
