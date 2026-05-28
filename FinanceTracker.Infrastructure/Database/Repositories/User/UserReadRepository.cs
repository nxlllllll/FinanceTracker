using System.Text.Json;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Domains.Operation;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Operation;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.User;

public sealed class UserReadRepository(
	FinanceTrackerContext context
) : IUserAuthRepository, IUserQueryRepository
{
	async Task<Core.Domains.User.User?> IUserAuthRepository.GetByIdAsync(
		Guid userId,
		CancellationToken ct = default)
	{
		return await context.Users.AsNoTracking().Where(predicate: u => u.Id == userId)
			.Select(selector: u => Core.Domains.User.User.Reconstitute(
				id: u.Id,
				email: u.Email,
				passwordHash: u.PasswordHash,
				baseCurrencyCode: u.BaseCurrencyCode,
				createdAt: u.CreatedAt
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<Core.Domains.User.User?> GetByEmailAsync(
		string email,
		CancellationToken ct = default)
	{
		return await context.Users.AsNoTracking().Where(predicate: u => u.Email == email)
			.Select(selector: u => Core.Domains.User.User.Reconstitute(
				id: u.Id,
				email: u.Email,
				passwordHash: u.PasswordHash,
				baseCurrencyCode: u.BaseCurrencyCode,
				createdAt: u.CreatedAt
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}
	
	async Task<UserReadModel?> IUserQueryRepository.GetByIdAsync(
		Guid userId,
		CancellationToken ct = default)
	{
		return await context.Users.AsNoTracking().Where(predicate: u => u.Id == userId)
			.Select(selector: u => new UserReadModel(
				Id: u.Id,
				Email: u.Email,
				BaseCurrency: u.BaseCurrencyCode,
				CreatedAt: u.CreatedAt
			)).FirstOrDefaultAsync(cancellationToken: ct);
	
	}

	public async Task<decimal> GetTotalBalanceAsync(
	    Guid userId,
	    Core.ValueObjects.Currency baseCurrency,
	    DateOnly date,
	    CancellationToken ct = default)
	{
	    return await context.Accounts.AsNoTracking().Where(predicate: a => a.UserId == userId && !a.IsArchived)
			.Join(
	            inner: context.AccountBalances,
	            outerKeySelector: a => a.Id,
	            innerKeySelector: b => b.AccountId,
	            resultSelector: (a, b) => new { a.Currency, b.Balance }
	        ).Select(selector: x => new
	        {
	            x.Currency,
	            x.Balance,
	            ExactRate = context.CurrencyRates.Where(r => r.BaseCode == x.Currency && r.TargetCode == baseCurrency.Value && r.ActualAt == date)
	                .Select(r => (decimal?)r.Rate)
					.FirstOrDefault(),
	            LatestRate = context.CurrencyRates.Where(r => r.BaseCode == x.Currency && r.TargetCode == baseCurrency.Value)
	                .OrderByDescending(r => r.ActualAt)
	                .Select(r => (decimal?)r.Rate)
	                .FirstOrDefault()
	        }).SumAsync(
				selector: x => x.Currency == baseCurrency.Value ? x.Balance : x.Balance * (x.ExactRate ?? x.LatestRate ?? 1m),
				cancellationToken: ct
			);
	}

	public async Task<(decimal Income, decimal Expense)> GetIncomeExpenseSummaryAsync(
		Guid userId,
		DateOnly period,
		CancellationToken ct = default)
	{
		var results = await context.CategoryTotals.AsNoTracking().Where(predicate: t => t.UserId == userId && t.Period == period)
			.Join(
				inner: context.Categories.Where(c => !c.IsArchived),
				outerKeySelector: t => t.CategoryId,
				innerKeySelector: c => c.Id,
				resultSelector: (t, c) => new { t.Total, c.Type }
			).GroupBy(keySelector: x => x.Type)
			.Select(selector: g => new { Type = g.Key, Sum = g.Sum(x => x.Total) })
			.ToListAsync(cancellationToken: ct);

		decimal income  = results.FirstOrDefault(predicate: x => x.Type == CategoryType.Income)?.Sum ?? 0;
		decimal expense = results.FirstOrDefault(predicate: x => x.Type == CategoryType.Expense)?.Sum ?? 0;

		return (income, expense);
	}

	public async Task<PagedResult<Core.ReadModels.Operation>> GetHistoryAsync(
		Guid userId,
		OperationFilterType? type = null,
		DateTimeOffset? dateFrom = null,
		DateTimeOffset? dateTo = null,
		DateTimeOffset? cursorOccurredAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default)
	{
		IQueryable<OperationEntity> query = type switch
		{
			OperationFilterType.Income => context.Operations
				.FromSql($"SELECT * FROM rm_operations WHERE user_id = {userId} AND type = 'Transaction' AND payload->>'Direction' = 'Credit'")
				.AsNoTracking(),
			OperationFilterType.Expense => context.Operations
				.FromSql($"SELECT * FROM rm_operations WHERE user_id = {userId} AND type = 'Transaction' AND payload->>'Direction' = 'Debit'")
				.AsNoTracking(),
			OperationFilterType.Transfer => context.Operations.AsNoTracking().Where(predicate: o => o.UserId == userId && o.Type == OperationType.Transfer),
			_ => context.Operations.AsNoTracking().Where(predicate: o => o.UserId == userId)
		};

		if (dateFrom is not null)
			query = query.Where(predicate: o => o.OccurredAt >= dateFrom);

		if (dateTo is not null)
			query = query.Where(predicate: o => o.OccurredAt <= dateTo);

		if (cursorOccurredAt is not null && cursorId is not null)
			query = query.Where(predicate: o => o.OccurredAt < cursorOccurredAt || o.OccurredAt == cursorOccurredAt && o.Id < cursorId);

		List<OperationEntity> entities = await query
			.OrderByDescending(keySelector: o => o.OccurredAt)
			.ThenByDescending(keySelector: o => o.Id)
			.Take(count: pageSize + 1)
			.ToListAsync(cancellationToken: ct);

		bool hasNextPage = entities.Count > pageSize;
		if (hasNextPage)
			entities.RemoveAt(entities.Count - 1);

		OperationEntity? last = entities.Count > 0 ? entities[^1] : null;

		List<Core.ReadModels.Operation> items = entities.Select(selector: e =>
		{
			OperationPayload payload = e.Type switch
			{
				OperationType.Transaction => JsonSerializer.Deserialize<TransactionPayload>(json: e.Payload, options: FinanceTrackerJsonOptions.Payload)!,
				OperationType.Transfer => JsonSerializer.Deserialize<TransferPayload>(json: e.Payload, options: FinanceTrackerJsonOptions.Payload)!,
				_ => throw new InvalidOperationException(message: $"Unknown operation type: {e.Type}")
			};

			return new Core.ReadModels.Operation(
				Id: e.Id,
				Type: payload is TransactionPayload tp
					? tp.Direction == Core.Domains.Account.DirectionType.Credit ? OperationFilterType.Income : OperationFilterType.Expense
					: OperationFilterType.Transfer,
				Description: e.Description,
				OccurredAt: e.OccurredAt,
				Transaction: payload is TransactionPayload txp ? new TransactionDetails(
					AccountId: txp.AccountId,
					CategoryId: txp.CategoryId,
					Amount: txp.Amount,
					Currency: txp.Currency,
					Direction: txp.Direction,
					IsExcluded: txp.IsExcluded
				) : null,
				Transfer: payload is TransferPayload trp ? new TransferDetails(
					FromAccountId: trp.FromAccountId,
					ToAccountId: trp.ToAccountId,
					AmountFrom: trp.AmountFrom,
					CurrencyFrom: trp.CurrencyFrom,
					AmountTo: trp.AmountTo,
					CurrencyTo: trp.CurrencyTo
				) : null
			);
		}).ToList();

		return new PagedResult<Core.ReadModels.Operation>(
			Items: items.AsReadOnly(),
			HasNextPage: hasNextPage,
			NextCursorDate: hasNextPage ? last?.OccurredAt : null,
			NextCursorId: hasNextPage ? last?.Id : null
		);
	}
}