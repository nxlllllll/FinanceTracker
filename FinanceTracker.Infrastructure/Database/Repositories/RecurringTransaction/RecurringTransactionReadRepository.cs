using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;

public sealed class RecurringTransactionReadRepository(
	FinanceTrackerContext context
) : IRecurringTransactionReadRepository
{
	public async Task<RecurringTransactionReadModel?> GetByIdAsync(
		Guid recurringTransactionId,
		CancellationToken ct = default)
	{
		return await context.RecurringTransactions.AsNoTracking()
			.Where(predicate: r => r.Id == recurringTransactionId)
			.Select(selector: r => new RecurringTransactionReadModel(
				Id: r.Id,
				UserId: r.UserId,
				AccountId: r.AccountId,
				CategoryId: r.CategoryId,
				Amount: Money.Reconstitute(amount: r.Amount, currency: r.Currency),
				Direction: r.Direction,
				DayOfMonth: r.DayOfMonth,
				Description: r.Description,
				IsActive: r.IsActive,
				RowVersion: r.RowVersion,
				LastExecutedAt: r.LastExecutedAt,
				CreatedAt: r.CreatedAt
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<PagedResult<RecurringTransactionReadModel>> GetByUserIdAsync(
		Guid userId,
		DateTimeOffset? cursorCreatedAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default)
	{
		List<string> where = ["user_id = $1"];
		List<object> args = [userId];

		if (cursorCreatedAt is not null && cursorId is not null)
		{
			args.Add(cursorCreatedAt.Value);
			args.Add(cursorId.Value);
			where.Add($"(created_at, id) < (${args.Count - 1}, ${args.Count})");
		}

		args.Add(pageSize + 1);

		string sql = $"""
			SELECT id, user_id, account_id, category_id, amount, currency_code,
			       direction_type, day_of_month, description, is_active,
			       row_version, last_executed_at, created_at
			FROM recurring_transactions
			WHERE {String.Join(separator: " AND ", values: where)}
			ORDER BY created_at DESC, id DESC
			LIMIT ${args.Count}
		""";

		await using NpgsqlConnection conn = await context.OpenReadConnectionAsync(ct: ct);
		await using NpgsqlCommand cmd = new NpgsqlCommand(cmdText: sql, connection: conn);
		foreach (object arg in args)
			cmd.Parameters.AddWithValue(value: arg);

		await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken: ct);

		List<RecurringTransactionReadModel> items = new List<RecurringTransactionReadModel>(capacity: pageSize + 1);
		while (await reader.ReadAsync(cancellationToken: ct))
			items.Add(item: MapRecurringTransaction(reader: reader));

		bool hasNextPage = items.Count > pageSize;
		if (hasNextPage) items.RemoveAt(index: items.Count - 1);

		RecurringTransactionReadModel? last = items.Count > 0 ? items[^1] : null;

		return new PagedResult<RecurringTransactionReadModel>(
			Items: items.AsReadOnly(),
			HasNextPage: hasNextPage,
			NextCursorDate: hasNextPage ? last?.CreatedAt : null,
			NextCursorId: hasNextPage ? last?.Id : null
		);
	}

	private static RecurringTransactionReadModel MapRecurringTransaction(NpgsqlDataReader reader)
	{
		Core.ValueObjects.Currency currency = Core.ValueObjects.Currency.Reconstitute(value: reader.GetString(ordinal: 5));
		Core.Domains.Account.DirectionType direction = Enum.Parse<Core.Domains.Account.DirectionType>(
			value: reader.GetString(ordinal: 6), ignoreCase: true);

		return new RecurringTransactionReadModel(
			Id: reader.GetGuid(ordinal: 0),
			UserId: reader.GetGuid(ordinal: 1),
			AccountId: reader.GetGuid(ordinal: 2),
			CategoryId: reader.GetGuid(ordinal: 3),
			Amount: Money.Reconstitute(amount: reader.GetDecimal(ordinal: 4), currency: currency),
			Direction: direction,
			DayOfMonth: reader.GetInt32(ordinal: 7),
			Description: reader.IsDBNull(ordinal: 8) ? null : reader.GetString(ordinal: 8),
			IsActive: reader.GetBoolean(ordinal: 9),
			RowVersion: reader.GetInt32(ordinal: 10),
			LastExecutedAt: reader.IsDBNull(ordinal: 11) ? null : reader.GetFieldValue<DateTimeOffset>(ordinal: 11),
			CreatedAt: reader.GetFieldValue<DateTimeOffset>(ordinal: 12)
		);
	}

	public async Task<IReadOnlyList<RecurringTransactionReadModel>> GetDueTodayAsync(
		int dayOfMonth,
		int daysInCurrentMonth,
		DateTimeOffset currentMonthStart,
		CancellationToken ct = default)
	{
		bool isLastDayOfMonth = dayOfMonth == daysInCurrentMonth;

		return await context.RecurringTransactions.AsNoTracking()
			.Where(predicate: r => r.IsActive &&
				(r.LastExecutedAt == null || r.LastExecutedAt < currentMonthStart) && 
				(r.DayOfMonth == dayOfMonth || isLastDayOfMonth && r.DayOfMonth > daysInCurrentMonth)
			).Select(selector: r => new RecurringTransactionReadModel(
				Id: r.Id,
				UserId: r.UserId,
				AccountId: r.AccountId,
				CategoryId: r.CategoryId,
				Amount: Money.Reconstitute(amount: r.Amount, currency: r.Currency),
				Direction: r.Direction,
				DayOfMonth: r.DayOfMonth,
				Description: r.Description,
				IsActive: r.IsActive,
				RowVersion: r.RowVersion,
				LastExecutedAt: r.LastExecutedAt,
				CreatedAt: r.CreatedAt
			)).ToListAsync(cancellationToken: ct);
	}
}