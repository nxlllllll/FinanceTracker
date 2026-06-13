using System.Runtime.CompilerServices;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinanceTracker.Infrastructure.Database.Repositories.User;

public sealed class UserReadRepository(
	FinanceTrackerContext context
) : IUserAuthRepository, IUserQueryRepository
{
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
				selector: x => x.Currency == baseCurrency ? x.Balance : x.Balance * (x.ExactRate ?? x.LatestRate ?? 1m),
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

	public async Task<PagedResult<Operation>> GetHistoryAsync(
		Guid userId,
		OperationFilterType? type = null,
		DateTimeOffset? dateFrom = null,
		DateTimeOffset? dateTo = null,
		DateTimeOffset? cursorOccurredAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default)
	{
		HistoryQuery query = HistoryQuery.Build(
			userId: userId,
			type: type,
			dateFrom: dateFrom,
			dateTo: dateTo,
			cursorOccurredAt: cursorOccurredAt,
			cursorId: cursorId,
			limit: pageSize + 1
		);

		await using NpgsqlConnection conn = await context.OpenReadConnectionAsync(ct: ct);
		await using NpgsqlCommand cmd = new NpgsqlCommand(cmdText: query.Sql, connection: conn);
		foreach (NpgsqlParameter param in query.Parameters)
			cmd.Parameters.Add(value: param);

		await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken: ct);

		List<Operation> items = new List<Operation>(capacity: pageSize + 1);
		while (await reader.ReadAsync(cancellationToken: ct))
			items.Add(item: HistoryRowMapper.MapFromReader(reader: reader));

		bool hasNextPage = items.Count > pageSize;
		if (hasNextPage)
			items.RemoveAt(index: items.Count - 1);

		Operation? last = items.Count > 0 ? items[^1] : null;

		return new PagedResult<Operation>(
			Items: [..items],
			HasNextPage: hasNextPage,
			NextCursorDate: hasNextPage ? last?.OccurredAt : null,
			NextCursorId: hasNextPage ? last?.Id : null
		);
	}
}