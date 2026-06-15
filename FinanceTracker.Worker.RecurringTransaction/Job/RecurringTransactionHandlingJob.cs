using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.RecurringTransaction;
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
        DateTimeOffset currentMonthStart = new DateTimeOffset(
            year: now.Year, month: now.Month, day: 1,
            hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero
        );

        logger.ZLogInformation(message: $"[{correlationContext.CorrelationId}] Processing due recurring transactions for {now:dd.MM.yyyy}.");

        int processed = 0;
        int failed = 0;
        
        await foreach (RecurringTransactionReadModel transaction in recurringTransactionReadRepository.GetDueTodayAsync(
            dayOfMonth: now.Day,
            daysInCurrentMonth: DateTime.DaysInMonth(year: now.Year, month: now.Month),
            currentMonthStart: currentMonthStart,
            ct: ct
        ))
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
                logger.ZLogError(exception: ex, message: $"[{correlationContext.CorrelationId}] Failed to process recurring transaction {transaction.Id}. Skipping.");
            }
        }

        if (failed > 0)
            logger.ZLogWarning(message: $"[{correlationContext.CorrelationId}] Completed with {failed} failure(s).");
    }
}