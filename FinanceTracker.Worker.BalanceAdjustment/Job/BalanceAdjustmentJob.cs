using System.Text.Json;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels.Pending;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Utilities.Retry;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Worker.Shared.Job;
using FinanceTracker.Worker.Shared.Metrics;
using Microsoft.Extensions.Options;
using Quartz;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Worker.BalanceAdjustment.Job;

[DisallowConcurrentExecution]
public sealed class BalanceAdjustmentJob(
	ITransactionReadRepository transactionReadRepository,
	ITransactionRepository transactionRepository,
	ITransactionWriteRepository transactionWriteRepository,
	ITransferReadRepository transferReadRepository,
	ITransferRepository transferRepository,
	ITransferWriteRepository transferWriteRepository,
	IAccountRepository accountRepository,
	ICurrencyRateReadRepository currencyRateReadRepository,
	IUnresolvableEventWriteRepository unresolvableEventWriteRepository,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider,
	IOptionsMonitor<BalanceAdjustmentJobOptions> options,
	ILogger<BalanceAdjustmentJob> logger
) : BaseJob<BalanceAdjustmentJobOptions>(options: options, logger: logger)
{
	private sealed record Outcome(AdjustResult Result, string? Reason = null);

	private sealed record PendingRate(
		Guid Id,
		Currency From,
		Currency To,
		DateTimeOffset OccurredAt,
		DateTimeOffset RateStatusChangedAt,
		Func<CancellationToken, Task<Settlement?>> LoadAsync
	);

	private sealed record Settlement(
		IRateSettleable Operation,
		Guid AccountId,
		DirectionType Direction,
		decimal Amount,
		Func<CancellationToken, Task> SaveRateResolutionAsync
	);

	private sealed class Tally
	{
		public int Resolved { get; set; }
		public int Approximated { get; set; }
		public int Unresolvable { get; set; }
		public int Waiting { get; set; }
		public int Failed { get; set; }
	}

	protected override async Task ProcessAsync(BalanceAdjustmentJobOptions options, CancellationToken ct)
	{
		await ProcessAsync(
			sourceType: AggregateTypeNames.Transaction,
			readPageAsync: async (cursorOccurredAt, cursorId, innerCt) =>
			{
				IReadOnlyList<PendingRateTransaction> page = await transactionReadRepository.GetPendingRateAsync(
					batchSize: options.BatchSize,
					cursorOccurredAt: cursorOccurredAt,
					cursorId: cursorId,
					ct: innerCt
				);

				return [..page.Select(selector: ToPendingRate)];
			},
			options: options,
			ct: ct
		);

		await ProcessAsync(
			sourceType: AggregateTypeNames.Transfer,
			readPageAsync: async (cursorOccurredAt, cursorId, innerCt) =>
			{
				IReadOnlyList<PendingRateTransfer> page = await transferReadRepository.GetPendingRateAsync(
					batchSize: options.BatchSize,
					cursorOccurredAt: cursorOccurredAt,
					cursorId: cursorId,
					ct: innerCt
				);

				return [.. page.Select(selector: ToPendingRate)];
			},
			options: options,
			ct: ct
		);
	}

	private async Task ProcessAsync(
		string sourceType,
		Func<DateTimeOffset?, Guid?, CancellationToken, Task<IReadOnlyList<PendingRate>>> readPageAsync,
		BalanceAdjustmentJobOptions options,
		CancellationToken ct)
	{
		Dictionary<Guid, Account> accountCache = new Dictionary<Guid, Account>();
		Tally tally = new Tally();

		DateTimeOffset? cursorOccurredAt = null;
		Guid? cursorId = null;

		while (!ct.IsCancellationRequested)
		{
			IReadOnlyList<PendingRate> page = await readPageAsync(cursorOccurredAt, cursorId, ct);

			if (page.Count == 0)
				break;

			foreach (PendingRate item in page)
			{
				if (ct.IsCancellationRequested)
					break;

				Outcome outcome = await RunWithRetryAsync(
					itemId: item.Id,
					accountCache: accountCache,
					options: options,
					work: innerCt => SettleAsync(
                        pending: item,
                        sourceType: sourceType,
                        accountCache: accountCache,
                        options: options,
                        ct: innerCt
                    ),
					ct: ct
				);

				Record(
                    tally: tally,
                    outcome: outcome,
                    sourceType: sourceType,
                    itemId: item.Id
                );
			}

			cursorOccurredAt = page[^1].OccurredAt;
			cursorId = page[^1].Id;

			if (page.Count < options.BatchSize)
				break;
		}

		LogSummary(entityName: sourceType, tally: tally);
	}

	private PendingRate ToPendingRate(PendingRateTransaction item) => new PendingRate(
		Id: item.TransactionId,
		From: item.TransactionCurrency,
		To: item.BaseCurrency,
		OccurredAt: item.OccurredAt,
		RateStatusChangedAt: item.RateStatusChangedAt,
		LoadAsync: async ct =>
		{
			Transaction? transaction = await transactionRepository.GetByIdAsync(transactionId: item.TransactionId, userId: item.UserId, ct: ct);

			return transaction is null ? null : new Settlement(
				Operation: transaction,
				AccountId: transaction.AccountId,
				Direction: transaction.Direction,
				Amount: transaction.Amount.Amount,
				SaveRateResolutionAsync: innerCt => transactionWriteRepository.SaveRateResolutionAsync(transaction: transaction, ct: innerCt)
			);
		}
	);

	private PendingRate ToPendingRate(PendingRateTransfer item) => new PendingRate(
		Id: item.TransferId,
		From: item.CurrencyFrom,
		To: item.CurrencyTo,
		OccurredAt: item.OccurredAt,
		RateStatusChangedAt: item.RateStatusChangedAt,
		LoadAsync: async ct =>
		{
			Transfer? transfer = await transferRepository.GetByIdAsync(transferId: item.TransferId, ct: ct);

			return transfer is null ? null : new Settlement(
				Operation: transfer,
				AccountId: transfer.ToAccountId,
				Direction: DirectionType.Credit,
				Amount: transfer.AmountFrom.Amount,
				SaveRateResolutionAsync: innerCt => transferWriteRepository.SaveRateResolutionAsync(transfer: transfer, ct: innerCt)
			);
		}
	);

	private async Task<Outcome> SettleAsync(
		PendingRate pending,
		string sourceType,
		Dictionary<Guid, Account> accountCache,
		BalanceAdjustmentJobOptions options,
		CancellationToken ct)
	{
		decimal? newRate = await currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: pending.From,
			targetCurrencyCode: pending.To,
			date: DateOnly.FromDateTime(dateTime: pending.OccurredAt.UtcDateTime),
			ct: ct
		);

		if (newRate is null && !HasOutlivedGrace(rateStatusChangedAt: pending.RateStatusChangedAt, options: options))
			return new Outcome(Result: AdjustResult.Waiting);

		Settlement? settlement = await pending.LoadAsync(arg: ct);

		if (settlement is null)
			return new Outcome(Result: AdjustResult.Waiting, Reason: $"{sourceType} disappeared between queue and settlement.");

		IRateSettleable operation = settlement.Operation;

		if (!operation.RateStatus.IsOpen())
			return new Outcome(Result: AdjustResult.Waiting, Reason: $"Rate already settled as {operation.RateStatus}.");

		if (newRate is null)
		{
			return await CloseAsync(
				settlement: settlement,
				reason: $"No rate for {pending.From} > {pending.To} on {pending.OccurredAt:d} after {options.RateGracePeriodDays} day(s).",
				ct: ct
			);
		}

		Account? account = await GetOrLoadAccountAsync(cache: accountCache, accountId: settlement.AccountId, ct: ct);
		if (account is null)
		{
			return await EscalateAsync(
				settlement: settlement,
				reason: $"Account {settlement.AccountId} not found.",
				payload: new { sourceType, sourceId = operation.Id, accountId = settlement.AccountId },
				ct: ct
			);
		}

		Result<Unit, DomainException> adjusted = account.AdjustBalance(
			occurredAt: dateProvider.UtcNow,
			sourceId: operation.Id,
			sourceType: sourceType,
			direction: settlement.Direction,
			oldRate: operation.ExchangeRate,
			newRate: newRate.Value,
			amount: settlement.Amount
		);

		if (adjusted.IsFailure)
		{
			return await EscalateAsync(
				settlement: settlement,
				reason: adjusted.Error!.Message,
				payload: new { sourceType, sourceId = operation.Id, accountId = account.Id, oldRate = operation.ExchangeRate, newRate = newRate.Value },
				ct: ct
			);
		}

		Result<Unit, DomainException> resolved = operation.ResolveRate(newRate: newRate.Value, changedAt: dateProvider.UtcNow);
		if (resolved.IsFailure)
		{
			accountCache.Remove(key: account.Id);

			return await EscalateAsync(
				settlement: settlement,
				reason: resolved.Error!.Message,
				payload: new { sourceType, sourceId = operation.Id, newRate = newRate.Value },
				ct: ct
			);
		}

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await accountRepository.SaveAsync(account: account, ct: ct);
			await settlement.SaveRateResolutionAsync(arg: ct);
		}, ct: ct);

		return new Outcome(Result: AdjustResult.Resolved);
	}

	/// <summary>Closes a row without touching any balance — the placeholder rate stands as final.</summary>
	private async Task<Outcome> CloseAsync(
		Settlement settlement,
		string reason,
		CancellationToken ct)
	{
		Result<Unit, DomainException> applied = settlement.Operation.ApproximateRate(changedAt: dateProvider.UtcNow);
		if (applied.IsFailure)
			return new Outcome(Result: AdjustResult.Failed, Reason: applied.Error!.Message);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () => await settlement.SaveRateResolutionAsync(arg: ct), ct: ct);

		return new Outcome(Result: AdjustResult.Approximated, Reason: reason);
	}

	/// <summary>
	/// Closes a row as <c>Unresolvable</c> and records why, in the same transaction. The two must land
	/// together: a row closed without a trail is a balance that is knowingly wrong and nobody knows it.
	/// </summary>
	private async Task<Outcome> EscalateAsync(
		Settlement settlement,
		string reason,
		object payload,
		CancellationToken ct)
	{
		Result<Unit, DomainException> applied = settlement.Operation.MarkRateUnresolvable(changedAt: dateProvider.UtcNow);
		if (applied.IsFailure)
			return new Outcome(Result: AdjustResult.Failed, Reason: applied.Error!.Message);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await unresolvableEventWriteRepository.CreateAsync(
				type: UnresolvableEventType.RateAdjustmentFailed,
				referenceId: settlement.Operation.Id,
				reason: reason,
				payload: JsonSerializer.Serialize(value: payload),
				occurredAt: dateProvider.UtcNow,
				ct: ct
			);

			await settlement.SaveRateResolutionAsync(arg: ct);
		}, ct: ct);

		return new Outcome(Result: AdjustResult.Unresolvable, Reason: reason);
	}

	private bool HasOutlivedGrace(DateTimeOffset rateStatusChangedAt, BalanceAdjustmentJobOptions options)
		=> dateProvider.UtcNow - rateStatusChangedAt > TimeSpan.FromDays(value: options.RateGracePeriodDays);

	private async Task<Outcome> RunWithRetryAsync(
		Guid itemId,
		Dictionary<Guid, Account> accountCache,
		BalanceAdjustmentJobOptions options,
		Func<CancellationToken, Task<Outcome>> work,
		CancellationToken ct)
	{
		try
		{
			return await RetryDelayCalculator.ExecuteWithRetryAsync(
				operation: work,
				logging: (exception, attempt, delay) =>
				{
					accountCache.Clear();

					logger.ZLogWarning(exception: exception, message: $"[ConcurrencyRetry] {itemId}: attempt {attempt + 1}/{options.MaxRetries} failed. Retrying in {delay}ms.");
				},
				maxRetries: options.MaxRetries,
				baseDelayMs: options.BaseDelayMs,
				useJitter: options.UseJitter,
				ct: ct
			);
		}
		catch (Exception ex)
		{
			accountCache.Clear();
			logger.ZLogError(exception: ex, message: $"Failed to settle {itemId}. It stays pending and will be retried on the next run.");
			return new Outcome(Result: AdjustResult.Failed, Reason: ex.Message);
		}
	}

	private async Task<Account?> GetOrLoadAccountAsync(
		Dictionary<Guid, Account> cache,
		Guid accountId,
		CancellationToken ct)
	{
		if (cache.TryGetValue(key: accountId, out Account? cached) && cached.Events.Count == 0)
			return cached;

		Account? loaded = await accountRepository.GetByIdAsync(accountId: accountId, ct: ct);

		if (loaded is not null)
			cache[accountId] = loaded;
		else
			cache.Remove(key: accountId);

		return loaded;
	}

	private void Record(Tally tally, Outcome outcome, string sourceType, Guid itemId)
	{
		KeyValuePair<string, object?> tag = new KeyValuePair<string, object?>(key: "source_type", value: sourceType.ToLowerInvariant());

		switch (outcome.Result)
		{
			case AdjustResult.Resolved:
				tally.Resolved++;
				WorkerMetrics.BalanceAdjustmentResolved.Add(delta: 1, tag);
				break;

			case AdjustResult.Approximated:
				tally.Approximated++;
				WorkerMetrics.BalanceAdjustmentApproximated.Add(delta: 1, tag);
				logger.ZLogWarning(message: $"[{sourceType}] {itemId}: rate written off as approximate. {outcome.Reason}");
				break;

			case AdjustResult.Unresolvable:
				tally.Unresolvable++;
				WorkerMetrics.BalanceAdjustmentUnresolvable.Add(delta: 1, tag);
				logger.ZLogError(message: $"[{sourceType}] {itemId}: rate settled but correction rejected — escalated to unresolvable_events. {outcome.Reason}");
				break;

			case AdjustResult.Waiting:
				tally.Waiting++;
				break;

			case AdjustResult.Failed:
				tally.Failed++;
				WorkerMetrics.BalanceAdjustmentFailed.Add(delta: 1, tag);
				break;
		}
	}

	private void LogSummary(string entityName, Tally tally)
	{
		logger.ZLogInformation(message: $"""
			{entityName}s settled. Resolved: {tally.Resolved}, approximated: {tally.Approximated},
			unresolvable: {tally.Unresolvable}, still waiting: {tally.Waiting}, failed: {tally.Failed}.
		""");
	}
}
