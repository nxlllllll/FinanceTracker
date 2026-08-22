using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.ReadModels.Pending;
using FinanceTracker.Core.ReadModels.Transfer;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Transfer;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transfer;

public sealed class TransferReadRepository(
	FinanceTrackerContext context,
	IDateProvider dateProvider
) : ITransferReadRepository
{
	public async Task<TransferReadModel?> GetByIdAsync(
		Guid transferId,
		CancellationToken ct = default)
	{
		return await context.Transfers.AsNoTracking().Where(predicate: t => t.Id == transferId).Select(selector: t => new TransferReadModel(
			Id: t.Id,
			UserId: t.UserId,
			FromAccountId: t.FromAccountId,
			ToAccountId: t.ToAccountId,
			AmountFrom: Money.Reconstitute(amount: t.AmountFrom, currency: t.CurrencyFrom),
			AmountTo: Money.Reconstitute(amount: t.AmountTo, currency: t.CurrencyTo),
			ExchangeRate: t.ExchangeRate,
			RateStatus: t.RateStatus,
			Status: t.Status,
			Description: t.Description,
			OccurredAt: t.OccurredAt
		)).FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<PagedResult<TransferReadModel>> GetAllAsync(
		Guid userId,
		Guid? accountId = null,
		DateTimeOffset? dateFrom = null,
		DateTimeOffset? dateTo = null,
		DateTimeOffset? cursorOccurredAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default)
	{
		IQueryable<TransferEntity> query = context.Transfers
												.AsNoTracking()
												.Where(predicate: t => t.UserId == userId);

		if (accountId is not null)
		{
			query = query.Where(predicate: t =>
				t.FromAccountId == accountId.Value ||
				t.ToAccountId == accountId.Value
			);
		}

		if (dateFrom is not null)
			query = query.Where(predicate: t => t.OccurredAt >= dateFrom.Value);

		if (dateTo is not null)
			query = query.Where(predicate: t => t.OccurredAt <= dateTo.Value);

		if (cursorOccurredAt is not null && cursorId is not null)
			query = query.Where(predicate: t => t.OccurredAt < cursorOccurredAt.Value || (t.OccurredAt == cursorOccurredAt.Value && t.Id < cursorId.Value));

		List<TransferReadModel> items = await query.OrderByDescending(keySelector: t => t.OccurredAt)
			.ThenByDescending(keySelector: t => t.Id)
			.Take(count: pageSize + 1)
			.Select(selector: t => new TransferReadModel(
				Id: t.Id,
				UserId: t.UserId,
				FromAccountId: t.FromAccountId,
				ToAccountId: t.ToAccountId,
				AmountFrom: Money.Reconstitute(amount: t.AmountFrom, currency: t.CurrencyFrom),
				AmountTo: Money.Reconstitute(amount: t.AmountTo, currency: t.CurrencyTo),
				ExchangeRate: t.ExchangeRate,
				RateStatus: t.RateStatus,
				Status: t.Status,
				Description: t.Description,
				OccurredAt: t.OccurredAt
			)).ToListAsync(cancellationToken: ct);

		bool hasNextPage = items.Count > pageSize;
		if (hasNextPage)
			items.RemoveAt(index: items.Count - 1);

		TransferReadModel? last = items.Count > 0 ? items[^1] : null;

		return new PagedResult<TransferReadModel>(
			Items: items.AsReadOnly(),
			HasNextPage: hasNextPage,
			NextCursorDate: hasNextPage ? last?.OccurredAt : null,
			NextCursorId: hasNextPage ? last?.Id : null
		);
	}

	public async Task<IReadOnlyList<PendingRateTransfer>> GetPendingRateAsync(
		int batchSize,
		DateTimeOffset? cursorOccurredAt = null,
		Guid? cursorId = null,
		CancellationToken ct = default)
	{
		IQueryable<TransferEntity> query = context.Transfers.AsNoTracking()
												.Where(predicate: t => t.RateStatus == RateStatus.Pending && t.Status == TransferStatus.Completed);

		if (cursorOccurredAt is not null && cursorId is not null)
		{
			query = query.Where(predicate: t =>
				t.OccurredAt > cursorOccurredAt.Value ||
				(t.OccurredAt == cursorOccurredAt.Value && t.Id > cursorId.Value)
			);
		}

		return await query.OrderBy(keySelector: t => t.OccurredAt)
			.ThenBy(keySelector: t => t.Id)
			.Take(count: batchSize)
			.Select(selector: t => new PendingRateTransfer(
				TransferId: t.Id,
				CurrencyFrom: t.CurrencyFrom,
				CurrencyTo: t.CurrencyTo,
				OccurredAt: t.OccurredAt,
				RateStatusChangedAt: t.RateStatusChangedAt
			)).ToListAsync(cancellationToken: ct);
	}

	/// <summary>Counts transfers whose credit side has been pending longer than <paramref name="gracePeriod"/>.</summary>
	public async Task<int> GetPendingCreditCountAsync(TimeSpan gracePeriod, CancellationToken ct = default)
	{
		DateTimeOffset threshold = dateProvider.UtcNow - gracePeriod;
		return await context.Transfers.AsNoTracking().CountAsync(
			predicate: t => t.Status == TransferStatus.PendingCredit && t.CreatedAt < threshold,
			cancellationToken: ct
		);
	}

	/// <summary>Selects transfers eligible for auto-compensation</summary>
	public async Task<IReadOnlyList<PendingCreditTransfer>> GetPendingCreditForCompensationAsync(
		TimeSpan compensationThreshold,
		CancellationToken ct = default)
	{
		DateTimeOffset threshold = dateProvider.UtcNow - compensationThreshold;
		return await context.Transfers.AsNoTracking()
			.Where(predicate: t => t.Status == TransferStatus.PendingCredit && t.CreatedAt < threshold)
			.Select(selector: t => new PendingCreditTransfer(
				TransferId: t.Id,
				FromAccountId: t.FromAccountId,
				Amount: t.AmountFrom,
				OccurredAt: t.OccurredAt
			)).ToListAsync(cancellationToken: ct);
	}

	public async Task<bool> HasOpenObligationAsync(Guid accountId, CancellationToken ct = default)
	{
		return await context.Transfers.AsNoTracking().AnyAsync(
			predicate: t =>
				(t.FromAccountId == accountId || t.ToAccountId == accountId) &&
				(t.Status == TransferStatus.PendingCredit || t.RateStatus == RateStatus.Pending),
			cancellationToken: ct
		);
	}
}
