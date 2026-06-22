using System.Text.Json;
using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Services.Correlation;
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
        DateTimeOffset currentMonthStart = new DateTimeOffset(year: now.Year, month: now.Month, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

        logger.ZLogInformation(message: $"[{correlationContext.CorrelationId}] Processing due recurring transactions for {now:G}.");

        await EscalateMissedTransactionsAsync(now: now, currentMonthStart: currentMonthStart, ct: ct);

        int processed = 0;
        int failed = 0;
        
        IReadOnlyList<RecurringTransactionReadModel> dueTransactions = await recurringTransactionReadRepository.GetDueTodayAsync(
            dayOfMonth: now.Day,
            daysInCurrentMonth: DateTime.DaysInMonth(year: now.Year, month: now.Month),
            currentMonthStart: currentMonthStart,
            ct: ct
        );

        foreach (RecurringTransactionReadModel transaction in dueTransactions)
        {
            try
            {
                Guid messageId = DeterministicGuid.Create(baseId: transaction.Id, year: now.Year, month: now.Month);

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
                    OccurredAt: now,
                    CorrelationId: correlationContext.CorrelationId
                ), correlationId: correlationContext.CorrelationId, ct: ct);

                await unitOfWork.ExecuteInTransactionAsync(operation: async () => await recurringTransactionWriteRepository.MarkExecutedAsync(
                    recurringTransactionId: transaction.Id,
                    executedAt: now,
                    expectedVersion: transaction.RowVersion,
                    ct: ct
                ), ct: ct);

                logger.ZLogInformation(message: $"[{correlationContext.CorrelationId}] Processed recurring transaction {transaction.Id} ({++processed}).");
            }
            catch (Exception ex)
            {
                failed++;
                logger.ZLogError(exception: ex, message: $"[{correlationContext.CorrelationId}] Failed to process recurring transaction {transaction.Id}. Will retry on the next run while today's window is still open.");
            }
        }

        if (failed > 0)
            logger.ZLogWarning(message: $"[{correlationContext.CorrelationId}] Completed with {failed} failure(s).");
    }

    private async Task EscalateMissedTransactionsAsync(DateTimeOffset now, DateTimeOffset currentMonthStart, CancellationToken ct)
    {
        DateTimeOffset previousMonthStart = currentMonthStart.AddMonths(months: -1);

        IReadOnlyList<RecurringTransactionReadModel> missed = await recurringTransactionReadRepository.GetMissedThisMonthAsync(
            dayOfMonth: now.Day,
            currentMonthStart: currentMonthStart,
            previousMonthStart: previousMonthStart,
            ct: ct
        );

        foreach (RecurringTransactionReadModel transaction in missed)
        {
            try
            {
                await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
                {
                    await unresolvableEventWriteRepository.CreateAsync(
                        type: UnresolvableEventType.RecurringTransactionFailed,
                        referenceId: transaction.Id,
                        reason: $"Scheduled for day {transaction.DayOfMonth}, no occurrence was ever executed; detected as missed on {now:dd.MM.yyyy}.",
                        payload: JsonSerializer.Serialize(value: new
                        {
                            recurringTransactionId = transaction.Id,
                            scheduledDayOfMonth = transaction.DayOfMonth,
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

                logger.ZLogError(message: $"[{correlationContext.CorrelationId}] Recurring transaction {transaction.Id} missed its occurrence (scheduled day {transaction.DayOfMonth}) — escalated to unresolvable_events.");
            }
            catch (Exception ex)
            {
                logger.ZLogError(exception: ex, message: $"[{correlationContext.CorrelationId}] Failed to escalate missed recurring transaction {transaction.Id}.");
            }
        }
    }
}