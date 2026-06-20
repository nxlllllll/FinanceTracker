using FinanceTracker.Core.Domains.User;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Category;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Extensions;

/// <summary>
/// EF Core <see cref="DbContext"/> extension methods for raw SQL operations
/// that cannot be expressed cleanly through the standard LINQ API.
/// </summary>
public static class DbContextExtensions
{
	private sealed record CurrencyRateRow(string BaseCode, string TargetCode, decimal Rate);
	
	/// <summary>
	/// Upserts a category total row — inserts or increments existing total and count atomically.
	/// Uses <c>ON CONFLICT DO UPDATE</c> to avoid lost updates under concurrent writes.
	/// </summary>
	public static Task UpsertCategoryTotalAsync(
		this DbContext context,
		CategoryTotalEntity entity,
		CancellationToken ct = default)
	{
		return context.Database.ExecuteSqlAsync(sql: $"""
			INSERT INTO rm_category_totals (id, user_id, category_id, period, total, transaction_count, row_version, updated_at)
			VALUES ({entity.Id}, {entity.UserId}, {entity.CategoryId}, {entity.Period}, {entity.Total}, {entity.TransactionCount}, 0, {entity.UpdatedAt})
			ON CONFLICT (user_id, category_id, period)
			DO UPDATE SET
				total = rm_category_totals.total + EXCLUDED.total,
				transaction_count = rm_category_totals.transaction_count + EXCLUDED.transaction_count,
				row_version = rm_category_totals.row_version + 1,
				updated_at = EXCLUDED.updated_at
			""", cancellationToken: ct);
	}

	/// <summary>
	/// Attempts to reserve an idempotency key using <c>INSERT … ON CONFLICT DO NOTHING</c>.
	/// Returns <c>true</c> if the key was newly inserted (this request owns it),
	/// or <c>false</c> if it already existed (duplicate request).
	/// </summary>
	public static async Task<bool> TryReserveIdempotentCommandAsync(
		this DbContext context,
		Guid idempotencyKey,
		string commandType,
		Guid userId,
		DateTimeOffset reservedAt,
		DateTimeOffset expiresAt,
		CancellationToken ct = default)
	{
		int rows = await context.Database.ExecuteSqlAsync(sql: $"""
			INSERT INTO idempotent_commands (idempotency_key, command_type, user_id, reserved_at, expires_at)
			VALUES ({idempotencyKey}, {commandType}, {userId}, {reservedAt}, {expiresAt})
			ON CONFLICT (idempotency_key, command_type, user_id) DO NOTHING
		""", cancellationToken: ct);

		return rows == 1;
	}

	/// <summary>
	/// Writes the serialized response JSON to a previously reserved idempotency record,
	/// marking the command as completed. Scoped to the same composite key as <see cref="TryReserveIdempotentCommandAsync"/>.
	/// </summary>
	public static Task CompleteIdempotentCommandAsync(
		this DbContext context,
		Guid idempotencyKey,
		string commandType,
		Guid userId,
		string responseJson,
		CancellationToken ct = default)
	{
		return context.Database.ExecuteSqlAsync(sql: $"""
			UPDATE idempotent_commands
			SET response_json = {responseJson}
			WHERE idempotency_key = {idempotencyKey} AND command_type = {commandType} AND user_id = {userId}
		""", cancellationToken: ct);
	}

	/// <summary>
	/// Loads a user session by refresh token hash using <c>SELECT … FOR UPDATE</c>
	/// to prevent concurrent refresh races.
	/// </summary>
	public static Task<UserSession?> GetSessionByRefreshTokenForUpdateAsync(
		this FinanceTrackerContext context,
		string tokenHash,
		CancellationToken ct = default)
	{
		return context.UserSessions.FromSqlRaw(sql: """
			SELECT * FROM user_sessions
			WHERE refresh_token_hash = {0}
			LIMIT 1
			FOR UPDATE
		""", tokenHash).Select(selector: u => UserSession.Reconstitute(
			id: u.Id,
			userId: u.UserId,
			refreshTokenHash: u.RefreshTokenHash,
			expiresAt: u.ExpiresAt,
			createdAt: u.CreatedAt,
			revokedAt: u.RevokedAt
		)).FirstOrDefaultAsync(cancellationToken: ct);
	}

	/// <summary>
	/// Loads the most recent exchange rate for each requested currency pair in a single query
	/// using <c>DISTINCT ON</c>.
	/// </summary>
	public static Task<Dictionary<(string BaseCode, string TargetCode), decimal>> GetLatestCurrencyRatesBatchAsync(
		this FinanceTrackerContext context,
		string[] fromCodes,
		string[] toCodes,
		CancellationToken ct = default)
	{
		return context.Database.SqlQuery<CurrencyRateRow>($"""
			SELECT DISTINCT ON (base_code, target_code) base_code AS BaseCode, target_code AS TargetCode, actual_at AS ActualAt, rate AS Rate
			FROM currency_rates
			WHERE base_code = ANY({fromCodes}) AND target_code = ANY({toCodes})
			ORDER BY base_code, target_code, actual_at DESC
		""").ToDictionaryAsync(
			keySelector: row => (row.BaseCode, row.TargetCode),
			elementSelector: row => row.Rate,
			cancellationToken: ct
		);
	}
}