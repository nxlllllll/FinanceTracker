using FinanceTracker.Core.Domains.User;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Category;
using FinanceTracker.Infrastructure.Database.Context.User;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Extensions;

public static class DbContextExtensions
{
	private sealed record CurrencyRateRow(string BaseCode, string TargetCode, decimal Rate);
	
	public static Task UpsertCategoryTotalAsync(
		this DbContext context,
		CategoryTotalEntity entity,
		CancellationToken ct = default)
	{
		return context.Database.ExecuteSqlAsync(sql: $"""
			INSERT INTO rm_category_totals (id, user_id, category_id, period, total, transaction_count, updated_at)
			VALUES ({entity.Id}, {entity.UserId}, {entity.CategoryId}, {entity.Period}, {entity.Total}, {entity.TransactionCount}, {entity.UpdatedAt})
			ON CONFLICT (user_id, category_id, period)
			DO UPDATE SET
				total = rm_category_totals.total + EXCLUDED.total,
				transaction_count = rm_category_totals.transaction_count + EXCLUDED.transaction_count,
				updated_at = EXCLUDED.updated_at
		""", cancellationToken: ct);
	}

	public static async Task<bool> TryReserveIdempotentCommandAsync(
		this DbContext context,
		Guid idempotencyKey,
		string commandType,
		DateTimeOffset createdAt,
		DateTimeOffset expiresAt,
		CancellationToken ct = default)
	{
		int rows = await context.Database.ExecuteSqlAsync(sql: $"""
			INSERT INTO idempotent_commands (idempotency_key, command_type, response_json, created_at, expires_at)
			VALUES ({idempotencyKey}, {commandType}, NULL, {createdAt}, {expiresAt})
			ON CONFLICT (idempotency_key) DO NOTHING
		""", cancellationToken: ct);

		return rows == 1;
	}

	public static Task CompleteIdempotentCommandAsync(
		this DbContext context,
		Guid idempotencyKey,
		string responseJson,
		CancellationToken ct = default)
	{
		return context.Database.ExecuteSqlAsync(sql: $"""
			UPDATE idempotent_commands
			SET response_json = {responseJson}
			WHERE idempotency_key = {idempotencyKey}
		""", cancellationToken: ct);
	}

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