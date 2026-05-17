using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.BalanceAdjustment.Jobs;

[DisallowConcurrentExecution]
public sealed class BalanceAdjustmentJob(
    ITransactionReadRepository transactionReadRepository,
    ITransactionWriteRepository transactionWriteRepository,
    ITransferReadRepository transferReadRepository,
    ITransferWriteRepository transferWriteRepository,
    IAccountRepository accountRepository,
    ICurrencyRateReadRepository currencyRateReadRepository,
    IUnitOfWork unitOfWork,
    ILogger<BalanceAdjustmentJob> logger
) : IJob
{
    public async Task Execute(IJobExecutionContext executionContext)
        => await ProcessAsync(ct: executionContext.CancellationToken);

    private async Task ProcessAsync(CancellationToken ct)
    {
        await ProcessTransactionsAsync(ct: ct);
        await ProcessTransfersAsync(ct: ct);
    }

    private async Task ProcessTransactionsAsync(CancellationToken ct)
    {
        IReadOnlyList<PendingRateTransaction> pending = await transactionReadRepository.GetPendingRateAsync(ct: ct);

        if (pending.Count == 0)
        {
            logger.ZLogInformation(message: $"No pending rate transactions found.");
            return;
        }

        logger.ZLogInformation(message: $"Processing {pending.Count} pending rate transaction(s).");

        int adjusted = 0;
        int failed = 0;

        foreach (PendingRateTransaction item in pending)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                decimal? newRate = await currencyRateReadRepository.GetRateAsync(
                    baseCurrencyCode: item.TransactionCurrency,
                    targetCurrencyCode: item.BaseCurrency,
                    date: DateOnly.FromDateTime(dateTime: item.OccurredAt),
                    ct: ct
                );

                if (newRate is null)
                {
                    logger.ZLogWarning(message: $"Rate not found for transaction {item.TransactionId} ({item.TransactionCurrency} → {item.BaseCurrency} on {item.OccurredAt:d}). Skipping.");
                    continue;
                }

                if (newRate == item.CurrentRate)
                {
                    await transactionWriteRepository.UpdateRateAsync(transactionId: item.TransactionId, newRate: newRate.Value, ct: ct);
                    adjusted++;
                    continue;
                }

                Account? account = await accountRepository.GetByIdAsync(accountId: item.AccountId, ct: ct);

                if (account is null)
                {
                    logger.ZLogWarning(message: $"Account {item.AccountId} not found for transaction {item.TransactionId}. Skipping.");
                    continue;
                }

                Result<Unit, Core.Exceptions.DomainExceptions.DomainException> adjustResult = account.AdjustBalance(
                    occurredAt: DateTime.UtcNow,
                    sourceId: item.TransactionId,
                    sourceType: AggregateTypeNames.Transaction,
                    direction: item.Direction,
                    oldRate: item.CurrentRate,
                    newRate: newRate.Value,
                    amount: item.Amount
                );

                if (adjustResult.IsFailure)
                {
                    logger.ZLogWarning(message: $"AdjustBalance failed for transaction {item.TransactionId}: {adjustResult.Error!.Message}. Skipping.");
                    continue;
                }

                await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
                {
                    await accountRepository.SaveAsync(account: account, ct: ct);
                    await transactionWriteRepository.UpdateRateAsync(transactionId: item.TransactionId, newRate: newRate.Value, ct: ct);
                }, ct: ct);

                adjusted++;
                logger.ZLogInformation(message: $"Adjusted transaction {item.TransactionId}: rate {item.CurrentRate} → {newRate}.");
            }
            catch (Exception ex)
            {
                failed++;
                logger.ZLogError(exception: ex, message: $"Failed to adjust transaction {item.TransactionId}.");
            }
        }

        logger.ZLogInformation(message: $"Transactions complete. Adjusted: {adjusted}, failed: {failed}.");
    }

    private async Task ProcessTransfersAsync(CancellationToken ct)
    {
        IReadOnlyList<PendingRateTransfer> pending = await transferReadRepository.GetPendingRateAsync(ct: ct);

        if (pending.Count == 0)
        {
            logger.ZLogInformation(message: $"No pending rate transfers found.");
            return;
        }

        logger.ZLogInformation(message: $"Processing {pending.Count} pending rate transfer(s).");

        int adjusted = 0;
        int failed = 0;

        foreach (PendingRateTransfer item in pending)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                decimal? newRate = await currencyRateReadRepository.GetRateAsync(
                    baseCurrencyCode: item.CurrencyFrom,
                    targetCurrencyCode: item.CurrencyTo,
                    date: DateOnly.FromDateTime(dateTime: item.OccurredAt),
                    ct: ct
                );

                if (newRate is null)
                {
                    logger.ZLogWarning(message: $"Rate not found for transfer {item.TransferId} ({item.CurrencyFrom} → {item.CurrencyTo} on {item.OccurredAt:d}). Skipping.");
                    continue;
                }

                if (newRate == item.CurrentRate)
                {
                    await transferWriteRepository.UpdateRateAsync(transferId: item.TransferId, newRate: newRate.Value, ct: ct);
                    adjusted++;
                    continue;
                }

                Account? fromAccount = await accountRepository.GetByIdAsync(accountId: item.FromAccountId, ct: ct);

                Account? toAccount = await accountRepository.GetByIdAsync(accountId: item.ToAccountId, ct: ct);

                if (fromAccount is null || toAccount is null)
                {
                    logger.ZLogWarning(message: $"Account(s) not found for transfer {item.TransferId}. Skipping.");
                    continue;
                }

                Result<Unit, Core.Exceptions.DomainExceptions.DomainException> fromResult = fromAccount.AdjustBalance(
                    occurredAt: DateTime.UtcNow,
                    sourceId: item.TransferId,
                    sourceType: AggregateTypeNames.Transfer,
                    direction: DirectionType.Debit,
                    oldRate: item.CurrentRate,
                    newRate: newRate.Value,
                    amount: item.AmountFrom
                );

                if (fromResult.IsFailure)
                {
                    logger.ZLogWarning(message: $"AdjustBalance failed for from-account on transfer {item.TransferId}: {fromResult.Error!.Message}. Skipping.");
                    continue;
                }

                Result<Unit, Core.Exceptions.DomainExceptions.DomainException> toResult = toAccount.AdjustBalance(
                    occurredAt: DateTime.UtcNow,
                    sourceId: item.TransferId,
                    sourceType: AggregateTypeNames.Transfer,
                    direction: DirectionType.Credit,
                    oldRate: item.CurrentRate,
                    newRate: newRate.Value,
                    amount: item.AmountFrom
                );

                if (toResult.IsFailure)
                {
                    logger.ZLogWarning(message: $"AdjustBalance failed for to-account on transfer {item.TransferId}: {toResult.Error!.Message}. Skipping.");
                    continue;
                }

                await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
                {
                    await accountRepository.SaveAsync(account: fromAccount, ct: ct);
                    await accountRepository.SaveAsync(account: toAccount, ct: ct);
                    await transferWriteRepository.UpdateRateAsync(transferId: item.TransferId, newRate: newRate.Value, ct: ct);
                }, ct: ct);

                adjusted++;
                logger.ZLogInformation(message: $"Adjusted transfer {item.TransferId}: rate {item.CurrentRate} → {newRate}.");
            }
            catch (Exception ex)
            {
                failed++;
                logger.ZLogError(exception: ex, message: $"Failed to adjust transfer {item.TransferId}.");
            }
        }

        logger.ZLogInformation(message: $"Transfers complete. Adjusted: {adjusted}, failed: {failed}.");
    }
}