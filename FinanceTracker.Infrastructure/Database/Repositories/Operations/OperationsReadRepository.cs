using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Operations;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Operations;

public sealed class OperationsReadRepository(
	FinanceTrackerContext context
) : IOperationsReadRepository
{
	private sealed record FlatOperation(
		Guid Id,
		OperationFilterType Type,
		string? Description,
		DateTime OccurredAt,
		Guid? AccountId,
		Guid? CategoryId,
		decimal? Amount,
		Core.ValueObjects.Currency? Currency,
		DirectionType? Direction,
		bool? IsExcluded,
		Guid? FromAccountId,
		Guid? ToAccountId,
		decimal? AmountFrom,
		Core.ValueObjects.Currency? CurrencyFrom,
		decimal? AmountTo,
		Core.ValueObjects.Currency? CurrencyTo
	);

	public async Task<IReadOnlyList<OperationDto>> GetHistoryAsync(
		Guid userId,
		OperationFilterType? type = null,
		DateTime? dateFrom = null,
		DateTime? dateTo = null,
		DateTime? cursorOccurredAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default)
	{
		IQueryable<FlatOperation> transactions = context.Transactions.AsNoTracking()
			.Where(predicate: t => t.UserId == userId)
			.Select(selector: t => new FlatOperation(
				Id: t.Id,
				Type: t.Direction == DirectionType.Credit ? OperationFilterType.Income : OperationFilterType.Expense,
				Description: t.Description,
				OccurredAt: t.OccurredAt,
				AccountId: t.AccountId,
				CategoryId: t.CategoryId,
				Amount: t.Amount,
				Currency: t.Currency,
				Direction: t.Direction,
				IsExcluded: t.IsExcluded,
				FromAccountId: null,
				ToAccountId: null,
				AmountFrom: null,
				CurrencyFrom: null,
				AmountTo: null,
				CurrencyTo: null
			));

		IQueryable<FlatOperation> transfers = context.Transfers.AsNoTracking()
			.Where(predicate: tr => tr.UserId == userId)
			.Select(selector: tr => new FlatOperation(
				Id: tr.Id,
				Type: OperationFilterType.Transfer,
				Description: tr.Description,
				OccurredAt: tr.OccurredAt,
				AccountId: null, 
				CategoryId: null, 
				Amount: null, 
				Currency: null, 
				Direction: null, 
				IsExcluded: null,
				FromAccountId: tr.FromAccountId,
				ToAccountId: tr.ToAccountId,
				AmountFrom: tr.AmountFrom,
				CurrencyFrom: tr.CurrencyFrom,
				AmountTo: tr.AmountTo,
				CurrencyTo: tr.CurrencyTo
			));

		IQueryable<FlatOperation> combined = transactions.Concat(source2: transfers);

		if (type is not null)
			combined = combined.Where(predicate: o => o.Type == type);

		if (dateFrom is not null)
			combined = combined.Where(predicate: o => o.OccurredAt >= dateFrom);

		if (dateTo is not null)
			combined = combined.Where(predicate: o => o.OccurredAt <= dateTo);

		if (cursorOccurredAt is not null && cursorId is not null)
			combined = combined.Where(predicate: o => o.OccurredAt < cursorOccurredAt || o.OccurredAt == cursorOccurredAt && o.Id < cursorId);

		List<FlatOperation> rows = await combined
			.OrderByDescending(keySelector: o => o.OccurredAt)
			.ThenByDescending(keySelector: o => o.Id)
			.Take(count: pageSize)
			.ToListAsync(cancellationToken: ct);

		return rows.Select(selector: o => new OperationDto(
			Id: o.Id,
			Type: o.Type,
			Description: o.Description,
			OccurredAt: o.OccurredAt,
			Transaction: o.AccountId is not null ? new TransactionDetailsDto(
				AccountId: o.AccountId.Value,
				CategoryId: o.CategoryId!.Value,
				Amount: o.Amount!.Value,
				Currency: o.Currency,
				Direction: o.Direction!.Value,
				IsExcluded: o.IsExcluded!.Value
			) : null,
			Transfer: o.FromAccountId is not null ? new TransferDetailsDto(
				FromAccountId: o.FromAccountId.Value,
				ToAccountId: o.ToAccountId!.Value,
				AmountFrom: o.AmountFrom!.Value,
				CurrencyFrom: o.CurrencyFrom!,
				AmountTo: o.AmountTo!.Value,
				CurrencyTo: o.CurrencyTo!
			) : null
		)).ToList();
	}
}