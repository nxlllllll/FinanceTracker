using System.Runtime.CompilerServices;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transaction;

public sealed class TransactionReadRepository(FinanceTrackerContext context) : ITransactionReadRepository
{
	public async Task<TransactionReadModel?> GetByIdAsync(
		Guid transactionId,
		Guid userId,
		CancellationToken ct = default)
	{
		return await context.Transactions.AsNoTracking().Where(predicate: t => t.Id == transactionId && t.UserId == userId)
			.Select(selector: t => new TransactionReadModel(
				Id: t.Id,
				AccountId: t.AccountId,
				UserId: t.UserId,
				CategoryId: t.CategoryId,
				Amount: Money.Reconstitute(amount: t.Amount, currency: t.Currency),
				Direction: t.Direction,
				ExchangeRate: t.ExchangeRate,
				IsExcluded: t.IsExcluded,
				IsRatePending: t.IsRatePending,
				Description: t.Description,
				OccurredAt: t.OccurredAt
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<PagedResult<TransactionReadModel>> GetAllAsync(
		Guid userId,
		Guid accountId,
		Guid? categoryId = null,
		DirectionType? direction = null,
		bool? isExcluded = null,
		DateTimeOffset? dateFrom = null,
		DateTimeOffset? dateTo = null,
		DateTimeOffset? cursorOccurredAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default)
	{
		(string sql, List<object> args) = BuildQuery(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			direction: direction,
			isExcluded: isExcluded,
			dateFrom: dateFrom,
			dateTo: dateTo,
			cursorOccurredAt: cursorOccurredAt,
			cursorId: cursorId,
			limit: pageSize + 1
		);

		await using NpgsqlConnection conn = await context.OpenReadConnectionAsync(ct: ct);
		await using NpgsqlCommand cmd = new NpgsqlCommand(cmdText: sql, connection: conn);
		foreach (object arg in args)
			cmd.Parameters.AddWithValue(value: arg);

		await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken: ct);

		List<TransactionReadModel> items = new List<TransactionReadModel>(capacity: pageSize + 1);
		while (await reader.ReadAsync(cancellationToken: ct))
			items.Add(item: MapTransaction(reader: reader));

		bool hasNextPage = items.Count > pageSize;
		if (hasNextPage)
			items.RemoveAt(index: items.Count - 1);

		TransactionReadModel? last = items.Count > 0 ? items[^1] : null;

		return new PagedResult<TransactionReadModel>(
			Items: items.AsReadOnly(),
			HasNextPage: hasNextPage,
			NextCursorDate: hasNextPage ? last?.OccurredAt : null,
			NextCursorId: hasNextPage ? last?.Id : null
		);
	}

	private static (string Sql, List<object> Args) BuildQuery(
		Guid userId,
		Guid accountId,
		Guid? categoryId,
		DirectionType? direction,
		bool? isExcluded,
		DateTimeOffset? dateFrom,
		DateTimeOffset? dateTo,
		DateTimeOffset? cursorOccurredAt,
		Guid? cursorId,
		int? limit)
	{
		List<string> where = ["account_id = $1", "user_id = $2"];
		List<object> args = [accountId, userId];

		if (categoryId is not null)
		{
			args.Add(categoryId.Value);
			where.Add($"category_id = ${args.Count}");
		}

		if (direction is not null)
		{
			args.Add(direction.Value.ToString().ToLowerInvariant());
			where.Add($"direction_type = ${args.Count}");
		}

		if (isExcluded is not null)
		{
			args.Add(isExcluded.Value);
			where.Add($"is_excluded = ${args.Count}");
		}

		if (dateFrom is not null)
		{
			args.Add(dateFrom.Value);
			where.Add($"occurred_at >= ${args.Count}");
		}

		if (dateTo is not null)
		{
			args.Add(dateTo.Value);
			where.Add($"occurred_at <= ${args.Count}");
		}

		if (cursorOccurredAt is not null && cursorId is not null)
		{
			args.Add(cursorOccurredAt.Value);
			args.Add(cursorId.Value);
			where.Add($"(occurred_at, id) < (${args.Count - 1}, ${args.Count})");
		}

		string limitClause = string.Empty;
		if (limit is not null)
		{
			args.Add(limit.Value);
			limitClause = $"LIMIT ${args.Count}";
		}

		string sql = $"""
			SELECT id, account_id, user_id, category_id, amount, currency_code,
			       direction_type, exchange_rate, is_excluded, is_rate_pending,
			       description, occurred_at
			FROM rm_transactions
			WHERE {String.Join(separator: " AND ", values: where)}
			ORDER BY occurred_at DESC, id DESC
			{limitClause}
		""";

		return (sql, args);
	}

	private static TransactionReadModel MapTransaction(NpgsqlDataReader reader)
	{
		Core.ValueObjects.Currency currency = Core.ValueObjects.Currency.Reconstitute(value: reader.GetString(ordinal: 5));
		DirectionType direction = Enum.Parse<DirectionType>(value: reader.GetString(ordinal: 6), ignoreCase: true);

		return new TransactionReadModel(
			Id: reader.GetGuid(ordinal: 0),
			AccountId: reader.GetGuid(ordinal: 1),
			UserId: reader.GetGuid(ordinal: 2),
			CategoryId: reader.GetGuid(ordinal: 3),
			Amount: Money.Reconstitute(amount: reader.GetDecimal(ordinal: 4), currency: currency),
			Direction: direction,
			ExchangeRate: reader.GetDecimal(ordinal: 7),
			IsExcluded: reader.GetBoolean(ordinal: 8),
			IsRatePending: reader.GetBoolean(ordinal: 9),
			Description: reader.IsDBNull(ordinal: 10) ? null : reader.GetString(ordinal: 10),
			OccurredAt: reader.GetFieldValue<DateTimeOffset>(ordinal: 11)
		);
	}

	public async Task<IReadOnlyList<PendingRateTransaction>> GetPendingRateAsync(CancellationToken ct = default)
	{
		return await context.Transactions.AsNoTracking().Where(predicate: t => t.IsRatePending).Join(
			inner: context.Users,
			outerKeySelector: t => t.UserId,
			innerKeySelector: u => u.Id,
			resultSelector: (t, u) => new PendingRateTransaction(
				TransactionId: t.Id,
				AccountId: t.AccountId,
				Amount: t.Amount,
				TransactionCurrency: t.Currency,
				BaseCurrency: u.BaseCurrencyCode,
				CurrentRate: t.ExchangeRate,
				Direction: t.Direction,
				RowVersion: t.RowVersion,
				OccurredAt: t.OccurredAt
			)
		).ToListAsync(cancellationToken: ct);
	}
}