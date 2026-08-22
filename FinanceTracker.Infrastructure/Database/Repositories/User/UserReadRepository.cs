using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.TransientExceptions;
using FinanceTracker.Core.ReadModels.Operation;
using FinanceTracker.Core.ReadModels.Transaction;
using FinanceTracker.Core.ReadModels.Transfer;
using FinanceTracker.Core.ReadModels.User;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Operation;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.User;

public sealed class UserReadRepository(
	FinanceTrackerContext context
) : IUserAuthRepository, IUserQueryRepository
{
	private sealed record AccountBalanceProjection(
		Core.ValueObjects.Currency Currency,
		decimal Balance,
		decimal? ExactRate,
		decimal? LatestRate
	);

	async Task<Core.Domains.User.User?> IUserAuthRepository.GetByIdAsync(
		Guid userId,
		CancellationToken ct)
	{
		return await context.Users.AsNoTracking().Where(predicate: u => u.Id == userId)
			.Select(selector: u => Core.Domains.User.User.Reconstitute(
				id: u.Id,
				email: u.Email,
				passwordHash: u.PasswordHash,
				baseCurrencyCode: u.BaseCurrencyCode,
				timeZone: u.TimeZoneId,
				rowVersion: u.RowVersion,
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
				timeZone: u.TimeZoneId,
				rowVersion: u.RowVersion,
				createdAt: u.CreatedAt
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}

	async Task<UserReadModel?> IUserQueryRepository.GetByIdAsync(
		Guid userId,
		CancellationToken ct)
	{
		return await context.Users.AsNoTracking().Where(predicate: u => u.Id == userId)
			.Select(selector: u => new UserReadModel(
				Id: u.Id,
				Email: u.Email,
				BaseCurrency: u.BaseCurrencyCode,
				CreatedAt: u.CreatedAt
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<TimeZoneId?> GetTimeZoneAsync(
		Guid userId,
		CancellationToken ct = default)
	{
		return await context.Users.AsNoTracking()
			.Where(predicate: u => u.Id == userId)
			.Select(selector: u => (TimeZoneId?)u.TimeZoneId)
			.FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<decimal> GetTotalBalanceAsync(
		Guid userId,
		Core.ValueObjects.Currency baseCurrency,
		DateOnly date,
		CancellationToken ct = default)
	{
		List<AccountBalanceProjection> accounts = await context.Accounts.AsNoTracking()
			.Where(predicate: a => a.UserId == userId && !a.IsArchived)
			.Join(
				inner: context.AccountBalances,
				outerKeySelector: a => a.Id,
				innerKeySelector: b => b.AccountId,
				resultSelector: (a, b) => new { a.Currency, b.Balance }
			).Select(selector: x => new AccountBalanceProjection(
				Currency: x.Currency,
				Balance: x.Balance,
				ExactRate: context.CurrencyRates.Where(r => r.BaseCode == x.Currency && r.TargetCode == baseCurrency.Value && r.ActualAt == date)
					.Select(r => (decimal?)r.Rate)
					.FirstOrDefault(),
				LatestRate: context.CurrencyRates.Where(r => r.BaseCode == x.Currency && r.TargetCode == baseCurrency.Value)
					.OrderByDescending(r => r.ActualAt)
					.Select(r => (decimal?)r.Rate)
					.FirstOrDefault()
			)).ToListAsync(cancellationToken: ct);

		return accounts.Sum(
			selector: x => x.Currency == baseCurrency ? x.Balance : x.Balance * (x.ExactRate ?? x.LatestRate ?? throw new CurrencyRateMissingException(
				message: $"No exchange rate found for {x.Currency.Value} to {baseCurrency.Value} — the total balance cannot be computed accurately.",
				fromCurrency: x.Currency,
				toCurrency: baseCurrency
			)
		));
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

		decimal income = results.FirstOrDefault(predicate: x => x.Type == CategoryType.Income)?.Sum ?? 0;
		decimal expense = results.FirstOrDefault(predicate: x => x.Type == CategoryType.Expense)?.Sum ?? 0;

		return (income, expense);
	}

	public async Task<PagedResult<Core.ReadModels.Operation.Operation>> GetHistoryAsync(
		Guid userId,
		OperationFilterType? type = null,
		DateTimeOffset? dateFrom = null,
		DateTimeOffset? dateTo = null,
		DateTimeOffset? cursorOccurredAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default)
	{
		IQueryable<OperationEntity> query = context.Operations
			.AsNoTracking()
			.Where(predicate: o => o.UserId == userId);

		query = type switch
		{
			OperationFilterType.Income => query.Where(predicate: o => o.Type == AggregateTypeNames.Transaction && o.DirectionType == "credit"),
			OperationFilterType.Expense => query.Where(predicate: o => o.Type == AggregateTypeNames.Transaction && o.DirectionType == "debit"),
			OperationFilterType.Transfer => query.Where(predicate: o => o.Type == AggregateTypeNames.Transfer),
			_ => query
		};

		if (dateFrom is not null)
			query = query.Where(predicate: o => o.OccurredAt >= dateFrom.Value);

		if (dateTo is not null)
			query = query.Where(predicate: o => o.OccurredAt <= dateTo.Value);

		if (cursorOccurredAt is not null && cursorId is not null)
			query = query.Where(predicate: o => o.OccurredAt < cursorOccurredAt.Value || (o.OccurredAt == cursorOccurredAt.Value && o.Id < cursorId.Value));

		List<OperationEntity> entities = await query
			.OrderByDescending(keySelector: o => o.OccurredAt)
			.ThenByDescending(keySelector: o => o.Id)
			.Take(count: pageSize + 1)
			.ToListAsync(cancellationToken: ct);

		bool hasNextPage = entities.Count > pageSize;
		if (hasNextPage)
			entities.RemoveAt(index: entities.Count - 1);

		IReadOnlyList<Core.ReadModels.Operation.Operation> items = entities.Select(selector: MapOperation).ToList().AsReadOnly();
		Core.ReadModels.Operation.Operation? last = items.Count > 0 ? items[^1] : null;

		return new PagedResult<Core.ReadModels.Operation.Operation>(
			Items: items,
			HasNextPage: hasNextPage,
			NextCursorDate: hasNextPage ? last?.OccurredAt : null,
			NextCursorId: hasNextPage ? last?.Id : null
		);
	}

	private static Core.ReadModels.Operation.Operation MapOperation(OperationEntity o)
	{
		bool isTransaction = o.Type == AggregateTypeNames.Transaction;

		if (!isTransaction)
		{
			return new Core.ReadModels.Operation.Operation(
				Id: o.Id,
				Type: OperationFilterType.Transfer,
				Description: o.Description,
				OccurredAt: o.OccurredAt,
				Transaction: null,
				Transfer: new TransferDetails(
					FromAccountId: o.FromAccountId!.Value,
					ToAccountId: o.ToAccountId!.Value,
					AmountFrom: o.AmountFrom!.Value,
					CurrencyFrom: Core.ValueObjects.Currency.Reconstitute(value: o.CurrencyFrom!),
					AmountTo: o.AmountTo!.Value,
					CurrencyTo: Core.ValueObjects.Currency.Reconstitute(value: o.CurrencyTo!),
					Status: o.Status!.FromCode()
				)
			);
		}

		DirectionType direction = Enum.Parse<DirectionType>(value: o.DirectionType!, ignoreCase: true);

		return new Core.ReadModels.Operation.Operation(
			Id: o.Id,
			Type: direction == DirectionType.Credit ? OperationFilterType.Income : OperationFilterType.Expense,
			Description: o.Description,
			OccurredAt: o.OccurredAt,
			Transaction: new TransactionDetails(
				AccountId: o.AccountId!.Value,
				CategoryId: o.CategoryId!.Value,
				Amount: o.Amount!.Value,
				Currency: Core.ValueObjects.Currency.Reconstitute(value: o.CurrencyCode!),
				Direction: direction,
				IsExcluded: o.IsExcluded!.Value
			),
			Transfer: null
		);

	}
}
