using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Utilities.Retry;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Worker.Shared.Job;
using FinanceTracker.Worker.Shared.Metrics;
using Microsoft.Extensions.Options;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.BalanceAdjustment.Job;

[DisallowConcurrentExecution]
public sealed class BalanceAdjustmentJob(
	ITransactionReadRepository transactionReadRepository,
	ITransactionWriteRepository transactionWriteRepository,
	ITransferReadRepository transferReadRepository,
	ITransferWriteRepository transferWriteRepository,
	IAccountRepository accountRepository,
	ICurrencyRateReadRepository currencyRateReadRepository,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider,
	IOptionsMonitor<BalanceAdjustmentJobOptions> options,
	ILogger<BalanceAdjustmentJob> logger
) : BaseJob<BalanceAdjustmentJobOptions>(options: options, logger: logger)
{
	/// <summary>
	/// Bundles the per-entity-type callbacks that <see cref="ProcessPendingAsync{T}"/> needs,
	/// so the shared loop takes one object instead of six loose Func parameters.
	/// </summary>
	private sealed record Strategy<T>(
		Func<T, Guid> GetId,
		Func<T, decimal> GetCurrentRate,
		Func<T, CancellationToken, Task<decimal?>> GetNewRateAsync,
		Func<T, string> BuildSkipMessage,
		Func<T, decimal, CancellationToken, Task> OnRateUnchangedAsync,
		Func<Dictionary<Guid, Account>, T, decimal, CancellationToken, Task<AdjustResult>> OnAdjustAsync
	);

	protected override async Task ProcessAsync(BalanceAdjustmentJobOptions options, CancellationToken ct)
	{
		await ProcessTransactionsAsync(options: options, ct: ct);
		await ProcessTransfersAsync(options: options, ct: ct);
	}
	
	private async Task ProcessPendingAsync<T>(
		IReadOnlyList<T> pending,
		BalanceAdjustmentJobOptions options,
		string entityName,
		Strategy<T> strategy,
		CancellationToken ct)
	{
		if (pending.Count == 0)
		{
			logger.ZLogInformation(message: $"No pending rate {entityName}s found.");
			return;
		}

		logger.ZLogInformation(message: $"Processing {pending.Count} pending rate {entityName}(s).");

		string sourceTypeTag = entityName.ToLowerInvariant();
		KeyValuePair<string, object?> sourceTag = new KeyValuePair<string, object?>(key: "source_type", value: sourceTypeTag);

		Dictionary<Guid, Account> accountCache = new Dictionary<Guid, Account>();

		int adjusted = 0;
		int skipped = 0;
		int failed = 0;

		foreach (T item in pending)
		{
			if (ct.IsCancellationRequested)
				break;

			decimal? newRate = await strategy.GetNewRateAsync(item, ct);

			if (newRate is null)
			{
				logger.ZLogWarning(message: $"{strategy.BuildSkipMessage(item)} Skipping.");
				skipped++;
				WorkerMetrics.BalanceAdjustmentSkipped.Add(delta: 1, sourceTag);
				continue;
			}

			if (newRate == strategy.GetCurrentRate(item))
			{
				await strategy.OnRateUnchangedAsync(item, newRate.Value, ct);
				adjusted++;
				WorkerMetrics.BalanceAdjustmentAdjusted.Add(delta: 1, sourceTag);
				continue;
			}

			AdjustResult result = await TryAdjustAsync(
				itemId: strategy.GetId(item),
				accountCache: accountCache,
				options: options,
				work: innerCt => strategy.OnAdjustAsync(accountCache, item, newRate.Value, innerCt),
				ct: ct
			);

			switch (result)
			{
				case AdjustResult.Adjusted:
					adjusted++;
					WorkerMetrics.BalanceAdjustmentAdjusted.Add(delta: 1, sourceTag);
					break;
				case AdjustResult.Skipped:
					skipped++;
					WorkerMetrics.BalanceAdjustmentSkipped.Add(delta: 1, sourceTag);
					break;
				case AdjustResult.Failed:
					failed++;
					WorkerMetrics.BalanceAdjustmentFailed.Add(delta: 1, sourceTag);
					break;
			}
		}

		LogSummary(entityName: entityName, total: pending.Count, adjusted: adjusted, skipped: skipped, failed: failed);
	}

	private async Task ProcessTransactionsAsync(BalanceAdjustmentJobOptions options, CancellationToken ct)
	{
		IReadOnlyList<PendingRateTransaction> pending = await transactionReadRepository.GetPendingRateAsync(ct: ct);

		await ProcessPendingAsync(
			pending: pending,
			options: options,
			entityName: nameof(Transaction),
			strategy: new Strategy<PendingRateTransaction>(
				GetId: item => item.AccountId,
				GetCurrentRate: item => item.CurrentRate,
				GetNewRateAsync: GetTransactionRateAsync,
				BuildSkipMessage: BuildTransactionSkipMessage,
				OnRateUnchangedAsync: UpdateTransactionRateAsync,
				OnAdjustAsync: AdjustTransactionAsync
			),
			ct: ct
		);
	}

	private Task<decimal?> GetTransactionRateAsync(PendingRateTransaction item, CancellationToken ct) => currencyRateReadRepository.GetRateAsync(
		baseCurrencyCode: item.TransactionCurrency,
		targetCurrencyCode: item.BaseCurrency,
		date: DateOnly.FromDateTime(dateTime: item.OccurredAt.UtcDateTime),
		ct: ct
	);

	private static string BuildTransactionSkipMessage(PendingRateTransaction item)
		=> $"Rate not found for transaction {item.TransactionId} ({item.TransactionCurrency} > {item.BaseCurrency} on {item.OccurredAt:d}).";

	private Task UpdateTransactionRateAsync(PendingRateTransaction item, decimal rate, CancellationToken ct) => transactionWriteRepository.UpdateRateAsync(
		transactionId: item.TransactionId,
		newRate: rate,
		expectedVersion: item.RowVersion,
		ct: ct
	);

	private async Task<AdjustResult> AdjustTransactionAsync(
		Dictionary<Guid, Account> accountCache,
		PendingRateTransaction item,
		decimal newRate,
		CancellationToken ct)
	{
		Account? account = await GetOrLoadAccountAsync(cache: accountCache, accountId: item.AccountId, ct: ct);

		if (account is null)
		{
			logger.ZLogWarning(message: $"Account {item.AccountId} not found for transaction {item.TransactionId}. Skipping.");
			return AdjustResult.Skipped;
		}

		Result<Unit, DomainException> adjustResult = account.AdjustBalance(
			occurredAt: dateProvider.UtcNow,
			sourceId: item.TransactionId,
			sourceType: AggregateTypeNames.Transaction,
			direction: item.Direction,
			oldRate: item.CurrentRate,
			newRate: newRate,
			amount: item.Amount
		);

		if (adjustResult.IsFailure)
		{
			logger.ZLogWarning(message: $"AdjustBalance failed for transaction {item.TransactionId}: {adjustResult.Error!.Message}. Skipping.");
			return AdjustResult.Skipped;
		}

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await accountRepository.SaveAsync(account: account, ct: ct);
			await transactionWriteRepository.UpdateRateAsync(
				transactionId: item.TransactionId,
				newRate: newRate,
				expectedVersion: item.RowVersion,
				ct: ct
			);
		}, ct: ct);

		logger.ZLogInformation(message: $"Adjusted transaction {item.TransactionId}: rate {item.CurrentRate} > {newRate}.");
		return AdjustResult.Adjusted;
	}

	private async Task ProcessTransfersAsync(BalanceAdjustmentJobOptions options, CancellationToken ct)
	{
		IReadOnlyList<PendingRateTransfer> pending = await transferReadRepository.GetPendingRateAsync(ct: ct);

		await ProcessPendingAsync(
			pending: pending,
			options: options,
			entityName: nameof(Transfer),
			strategy: new Strategy<PendingRateTransfer>(
				GetId: item => item.ToAccountId,
				GetCurrentRate: item => item.CurrentRate,
				GetNewRateAsync: GetTransferRateAsync,
				BuildSkipMessage: BuildTransferSkipMessage,
				OnRateUnchangedAsync: UpdateTransferRateAsync,
				OnAdjustAsync: AdjustTransferAsync
			),
			ct: ct
		);
	}

	private Task<decimal?> GetTransferRateAsync(PendingRateTransfer item, CancellationToken ct) => currencyRateReadRepository.GetRateAsync(
		baseCurrencyCode: item.CurrencyFrom,
		targetCurrencyCode: item.CurrencyTo,
		date: DateOnly.FromDateTime(dateTime: item.OccurredAt.UtcDateTime),
		ct: ct
	);

	private static string BuildTransferSkipMessage(PendingRateTransfer item)
		=> $"Rate not found for transfer {item.TransferId} ({item.CurrencyFrom} > {item.CurrencyTo} on {item.OccurredAt:d}).";

	private Task UpdateTransferRateAsync(PendingRateTransfer item, decimal rate, CancellationToken ct) => transferWriteRepository.UpdateRateAsync(
		transferId: item.TransferId,
		newRate: rate,
		expectedVersion: item.RowVersion,
		ct: ct
	);

	private async Task<AdjustResult> AdjustTransferAsync(
		Dictionary<Guid, Account> accountCache,
		PendingRateTransfer item,
		decimal newRate,
		CancellationToken ct)
	{
		Account? toAccount = await GetOrLoadAccountAsync(cache: accountCache, accountId: item.ToAccountId, ct: ct);

		if (toAccount is null)
		{
			logger.ZLogWarning(message: $"toAccount {item.ToAccountId} not found for transfer {item.TransferId}. Skipping.");
			return AdjustResult.Skipped;
		}

		Result<Unit, DomainException> toResult = toAccount.AdjustBalance(
			occurredAt: dateProvider.UtcNow,
			sourceId: item.TransferId,
			sourceType: AggregateTypeNames.Transfer,
			direction: DirectionType.Credit,
			oldRate: item.CurrentRate,
			newRate: newRate,
			amount: item.AmountFrom
		);

		if (toResult.IsFailure)
		{
			logger.ZLogWarning(message: $"AdjustBalance failed for to-account on transfer {item.TransferId}: {toResult.Error!.Message}. Skipping.");
			return AdjustResult.Skipped;
		}

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await accountRepository.SaveAsync(account: toAccount, ct: ct);
			await transferWriteRepository.UpdateRateAsync(
				transferId: item.TransferId,
				newRate: newRate,
				expectedVersion: item.RowVersion,
				ct: ct
			);
		}, ct: ct);

		logger.ZLogInformation(message: $"Adjusted transfer {item.TransferId}: rate {item.CurrentRate} > {newRate}.");
		return AdjustResult.Adjusted;
	}

	private async Task<AdjustResult> TryAdjustAsync(
		Guid itemId,
		Dictionary<Guid, Account> accountCache,
		BalanceAdjustmentJobOptions options,
		Func<CancellationToken, Task<AdjustResult>> work,
		CancellationToken ct)
	{
		try
		{
			return await RetryDelayCalculator.ExecuteWithRetryAsync(
				operation: work,
				logging: (exception, attempt, delay) =>
				{
					accountCache.Remove(key: itemId);
					logger.ZLogWarning(exception: exception, message: $"""
						[ConcurrencyRetry] Attempt {attempt + 1}/{options.MaxRetries} failed.
						Retrying in {delay}ms.
					""");
				},
				maxRetries: options.MaxRetries,
				baseDelayMs: options.BaseDelayMs,
				useJitter: options.UseJitter,
				ct: ct
			);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to adjust item {itemId}.");
			return AdjustResult.Failed;
		}
	}

	private void LogSummary(string entityName, int total, int adjusted, int skipped, int failed)
		=> logger.ZLogInformation(message: $"{entityName}s complete. Total: {total}, adjusted: {adjusted}, skipped: {skipped}, failed: {failed}.");

	private async Task<Account?> GetOrLoadAccountAsync(
		Dictionary<Guid, Account> cache,
		Guid accountId,
		CancellationToken ct)
	{
		if (cache.TryGetValue(key: accountId, out Account? cached))
			return cached;

		Account? loaded = await accountRepository.GetByIdAsync(accountId: accountId, ct: ct);
		if (loaded is not null)
			cache[accountId] = loaded;

		return loaded;
	}
}