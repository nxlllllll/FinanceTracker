using System.Text;
using System.Text.Json;
using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Worker.Shared.RabbitMQ;
using Microsoft.Extensions.Options;
using Quartz;
using RabbitMQ.Client;
using ZLogger;

namespace FinanceTracker.Worker.RecurringTransaction.Jobs;

[DisallowConcurrentExecution]
public sealed class RecurringTransactionHandlingJob(
    IRecurringTransactionReadRepository recurringTransactionReadRepository,
    IDateProvider dateProvider,
    RabbitMqConnectionFactory connectionFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<RecurringTransactionHandlingJob> logger
) : IJob
{
    private readonly RabbitMqOptions _options = options.Value;
    
    public async Task Execute(IJobExecutionContext executionContext)
    {
        await using IConnection connection = await connectionFactory.CreateConnectionAsync(ct: executionContext.CancellationToken);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: executionContext.CancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            cancellationToken: executionContext.CancellationToken
        );

        await ProcessTransactionsAsync(channel: channel, ct: executionContext.CancellationToken);
    }

    private async Task ProcessTransactionsAsync(IChannel channel, CancellationToken ct)
    {
        IReadOnlyList<Core.Domains.RecurringTransaction.RecurringTransaction> dueTransactions = await GetDueTransactionsAsync(ct: ct);

        if (dueTransactions.Count == 0)
            return;

        logger.ZLogInformation(message: $"Found {dueTransactions.Count} due recurring transaction(s) for {dateProvider.UtcNow:dd.MM.yyyy}.");

        int processed = 0;
        foreach (Core.Domains.RecurringTransaction.RecurringTransaction dueTransaction in dueTransactions)
        {
            await PublishAsync(channel: channel, transaction: dueTransaction, ct: ct);
            logger.ZLogInformation(message: $"Recurring transaction published: {++processed}/{dueTransactions.Count}.");
        }
    }

    private async Task<IReadOnlyList<Core.Domains.RecurringTransaction.RecurringTransaction>> GetDueTransactionsAsync(CancellationToken ct)
    {
        DateTime now = dateProvider.UtcNow;
        DateTime currentMonthStart = new DateTime(year: now.Year, month: now.Month, day: 1, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc);
        logger.ZLogInformation(message: $"Querying due transactions for day {now.Day}, month start: {currentMonthStart:O}, kind: {currentMonthStart.Kind}");
        return await recurringTransactionReadRepository.GetDueTodayAsync(
            dayOfMonth: now.Day,
            daysInCurrentMonth: DateTime.DaysInMonth(year: now.Year, month: now.Month),
            currentMonthStart: currentMonthStart,
            ct: ct
        );
    }

    private async Task PublishAsync(
        IChannel channel,
        Core.Domains.RecurringTransaction.RecurringTransaction transaction,
        CancellationToken ct)
    {
        RecurringTransactionTriggeredMessage message = new RecurringTransactionTriggeredMessage(
            MessageId: Guid.CreateVersion7(),
            RecurringTransactionId: transaction.Id,
            AccountId: transaction.AccountId,
            UserId: transaction.UserId,
            CategoryId: transaction.CategoryId,
            Amount: transaction.Amount.Amount,
            Currency: transaction.Amount.Currency,
            Direction: transaction.Direction.ToString(),
            Description: transaction.Description,
            OccurredAt: dateProvider.UtcNow
        );
        
        await channel.BasicPublishAsync(
            exchange: _options.ExchangeName,
            routingKey: String.Empty,
            body: Encoding.UTF8.GetBytes(s: JsonSerializer.Serialize(value: message, options: FinanceTrackerJsonOptions.Payload)),
            cancellationToken: ct
        );
    }
}