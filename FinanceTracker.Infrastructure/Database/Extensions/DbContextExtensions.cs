using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Repositories.Category;
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
	private sealed record CurrencyStableRateRow(string BaseCode, string TargetCode, DateTime AsOfUtc, decimal Rate);

	private sealed record TransactionRateRow(Guid Id, Guid CategoryId, DateOnly Period, decimal Amount, string CurrencyCode, decimal? Rate);

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
		Guid reservationId,
		DateTimeOffset reservedAt,
		DateTimeOffset expiresAt,
		CancellationToken ct = default)
	{
		int rows = await context.Database.ExecuteSqlAsync(sql: $"""
			INSERT INTO idempotent_commands (idempotency_key, command_type, user_id, reservation_id, reserved_at, expires_at)
			VALUES ({idempotencyKey}, {commandType}, {userId}, {reservationId}, {reservedAt}, {expiresAt})
			ON CONFLICT (idempotency_key, command_type, user_id) DO NOTHING
		""", cancellationToken: ct);

		return rows == 1;
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
			revokedAt: u.RevokedAt,
			supersededBySessionId: u.SupersededBySessionId
		)).FirstOrDefaultAsync(cancellationToken: ct);
	}

	public static Task<UserSession?> GetSessionByIdForUpdateAsync(
		this FinanceTrackerContext context,
		Guid sessionId,
		CancellationToken ct = default)
	{
		return context.UserSessions.FromSqlRaw(sql: """
			SELECT * FROM user_sessions
			WHERE id = {0}
			LIMIT 1
			FOR UPDATE
		""", sessionId).Select(selector: u => UserSession.Reconstitute(
			id: u.Id,
			userId: u.UserId,
			refreshTokenHash: u.RefreshTokenHash,
			expiresAt: u.ExpiresAt,
			createdAt: u.CreatedAt,
			revokedAt: u.RevokedAt,
			supersededBySessionId: u.SupersededBySessionId
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
			SELECT DISTINCT ON (base_code, target_code) base_code AS BaseCode, target_code AS TargetCode, rate AS Rate
			FROM currency_rates
			WHERE base_code = ANY({fromCodes}) AND target_code = ANY({toCodes})
			ORDER BY base_code, target_code, actual_at DESC
		""").ToDictionaryAsync(
			keySelector: row => (row.BaseCode, row.TargetCode),
			elementSelector: row => row.Rate,
			cancellationToken: ct
		);
	}

	public static Task<Dictionary<(string BaseCode, string TargetCode, DateTime AsOfUtc), decimal>> GetCurrencyRatesKnownAtOrBeforeBatchAsync(
		this FinanceTrackerContext context,
		string[] fromCodes,
		string[] toCodes,
		DateTime[] asOfUtc,
		CancellationToken ct = default)
	{
		return context.Database.SqlQuery<CurrencyStableRateRow>($"""
			SELECT q.base_code AS BaseCode, q.target_code AS TargetCode, q.as_of AS AsOfUtc, r.rate AS Rate
			FROM unnest({fromCodes}, {toCodes}, {asOfUtc}) AS q(base_code, target_code, as_of)
			JOIN LATERAL (
				SELECT cr.rate
				FROM currency_rates cr
				WHERE cr.base_code = q.base_code AND cr.target_code = q.target_code AND cr.created_at <= q.as_of
				ORDER BY cr.created_at DESC
				LIMIT 1
			) r ON true
		""").ToDictionaryAsync(
			keySelector: row => (row.BaseCode, row.TargetCode, row.AsOfUtc),
			elementSelector: row => row.Rate,
			cancellationToken: ct
		);
	}

	public static async Task<List<TransactionRateDto>> GetTransactionRatesForRecalculationPageAsync(
		this FinanceTrackerContext context,
		Guid userId,
		string baseCurrencyCode,
		Guid afterId,
		int batchSize,
		CancellationToken ct = default)
	{
		return await context.Database.SqlQuery<TransactionRateDto>($"""
			SELECT
				t.id AS Id,
				t.category_id AS CategoryId,
				date_trunc('month', t.occurred_at)::date AS Period,
				t.amount AS Amount,
				t.currency_code AS CurrencyCode,
				CASE WHEN t.currency_code = {baseCurrencyCode} THEN 1 ELSE r.rate END AS Rate
			FROM rm_transactions t
			LEFT JOIN LATERAL (
				SELECT cr.rate
				FROM currency_rates cr
				WHERE cr.base_code = t.currency_code AND cr.target_code = {baseCurrencyCode} AND cr.created_at <= t.occurred_at
				ORDER BY cr.created_at DESC
				LIMIT 1
			) r ON true
			WHERE t.user_id = {userId} AND NOT t.is_excluded AND t.id > {afterId}
			ORDER BY t.id
			LIMIT {batchSize}
		""").ToListAsync(cancellationToken: ct);
	}

	public static Task InsertAccountAsync(
		this DbContext context,
		Guid id,
		Guid userId,
		string name,
		string accountTypeCode,
		string currencyCode,
		bool isArchived,
		int lastVersion,
		DateTimeOffset createdAt,
		CancellationToken ct = default)
	{
		return context.Database.ExecuteSqlAsync(sql: $"""
			INSERT INTO accounts (id, user_id, name, account_type_code, currency_code, is_archived, last_version, created_at)
			VALUES ({id}, {userId}, {name}, {accountTypeCode}, {currencyCode}, {isArchived}, {lastVersion}, {createdAt})
			ON CONFLICT (id) DO NOTHING
		""", cancellationToken: ct);
	}

	public static Task InsertAccountBalanceAsync(
		this DbContext context,
		Guid accountId,
		decimal balance,
		int lastVersion,
		DateTimeOffset updatedAt,
		CancellationToken ct = default)
	{
		return context.Database.ExecuteSqlAsync(sql: $"""
			INSERT INTO rm_account_balances (account_id, balance, last_version, updated_at)
			VALUES ({accountId}, {balance}, {lastVersion}, {updatedAt})
			ON CONFLICT (account_id) DO NOTHING
		""", cancellationToken: ct);
	}

	public static async Task<bool> TryRecordAccountBalanceEventAppliedAsync(
		this DbContext context,
		Guid accountId,
		int version,
		DateTimeOffset appliedAt,
		CancellationToken ct = default)
	{
		int rows = await context.Database.ExecuteSqlAsync(sql: $"""
			INSERT INTO rm_account_balance_applied_events (account_id, version, applied_at)
			VALUES ({accountId}, {version}, {appliedAt})
			ON CONFLICT (account_id, version) DO NOTHING
		""", cancellationToken: ct);

		return rows == 1;
	}

	public static Task UpsertCurrencyRatesAsync(
		this DbContext context,
		string[] baseCodes,
		string[] targetCodes,
		decimal[] rateValues,
		DateOnly[] actualAtDates,
		DateTimeOffset createdAt,
		CancellationToken ct = default)
	{
		return context.Database.ExecuteSqlAsync(sql: $"""
			INSERT INTO currency_rates (base_code, target_code, rate, actual_at, created_at)
			SELECT base_code, target_code, rate, actual_at, {createdAt}
			FROM unnest({baseCodes}, {targetCodes}, {rateValues}, {actualAtDates}) AS u(base_code, target_code, rate, actual_at)
			ON CONFLICT (base_code, target_code, actual_at) DO NOTHING
		""", cancellationToken: ct);
	}

	public static Task GrantUserPermissionAsync(
		this DbContext context,
		Guid userId,
		string permission,
		DateTimeOffset grantedAt,
		int version,
		CancellationToken ct = default)
	{
		return context.Database.ExecuteSqlAsync(sql: $"""
			INSERT INTO user_permissions (user_id, permission, granted_at, last_version, is_active)
			VALUES ({userId}, {permission}, {grantedAt}, {version}, TRUE)
			ON CONFLICT (user_id, permission) DO UPDATE
			SET granted_at = EXCLUDED.granted_at,
			    last_version = EXCLUDED.last_version,
			    is_active = TRUE,
			    revoked_at = NULL
			WHERE user_permissions.last_version < EXCLUDED.last_version
		""", cancellationToken: ct);
	}

	public static Task RevokeUserPermissionAsync(
		this DbContext context,
		Guid userId,
		string permission,
		DateTimeOffset revokedAt,
		int version,
		CancellationToken ct = default)
	{
		return context.Database.ExecuteSqlAsync(sql: $"""
			INSERT INTO user_permissions (user_id, permission, granted_at, last_version, is_active, revoked_at)
			VALUES ({userId}, {permission}, {revokedAt}, {version}, FALSE, {revokedAt})
			ON CONFLICT (user_id, permission) DO UPDATE
			SET last_version = EXCLUDED.last_version,
			    is_active = FALSE,
			    revoked_at = EXCLUDED.revoked_at
			WHERE user_permissions.last_version < EXCLUDED.last_version
		""", cancellationToken: ct);
	}
//
// 	public static Task<int> DeleteOldPermissionTombstonesAsync(
// 		this DbContext context,
// 		DateTimeOffset before,
// 		int batchSize,
// 		CancellationToken ct = default)
// 	{
// 		return context.Database.ExecuteSqlAsync(sql: $"""
// 			DELETE FROM user_permissions
// 			USING (
// 				SELECT user_id, permission
// 				FROM user_permissions
// 				WHERE NOT is_active AND revoked_at < {before}
// 				ORDER BY revoked_at
// 				LIMIT {batchSize}
// 			) old
// 			WHERE user_permissions.user_id    = old.user_id
// 			  AND user_permissions.permission = old.permission
// 		""", cancellationToken: ct);
// 	}
//
// 	public static Task<int> DeleteOldMembershipTombstonesAsync(
// 		this DbContext context,
// 		DateTimeOffset before,
// 		int batchSize,
// 		CancellationToken ct = default)
// 	{
// 		return context.Database.ExecuteSqlAsync(sql: $"""
// 			DELETE FROM user_roles
// 			USING (
// 				SELECT user_id, role_id
// 				FROM user_roles
// 				WHERE NOT is_active AND removed_at < {before}
// 				ORDER BY removed_at
// 				LIMIT {batchSize}
// 			) old
// 			WHERE user_roles.user_id = old.user_id
// 			  AND user_roles.role_id = old.role_id
// 		""", cancellationToken: ct);
// 	}

	public static Task AssignUserRoleAsync(
		this DbContext context,
		Guid userId,
		Guid roleId,
		Guid assignedBy,
		DateTimeOffset assignedAt,
		int version,
		CancellationToken ct = default)
	{
		return context.Database.ExecuteSqlAsync(sql: $"""
			INSERT INTO user_roles (user_id, role_id, assigned_at, assigned_by, last_version, is_active)
			VALUES ({userId}, {roleId}, {assignedAt}, {assignedBy}, {version}, TRUE)
			ON CONFLICT (user_id, role_id) DO UPDATE
			SET assigned_at = EXCLUDED.assigned_at,
			    assigned_by = EXCLUDED.assigned_by,
			    last_version = EXCLUDED.last_version,
			    is_active = TRUE,
			    removed_at = NULL,
			    removed_by = NULL
			WHERE user_roles.last_version < EXCLUDED.last_version
		""", cancellationToken: ct);
	}

	public static Task RemoveUserRoleAsync(
		this DbContext context,
		Guid userId,
		Guid roleId,
		Guid removedBy,
		DateTimeOffset removedAt,
		int version,
		CancellationToken ct = default)
	{
		return context.Database.ExecuteSqlAsync(sql: $"""
			INSERT INTO user_roles (user_id, role_id, assigned_at, last_version, is_active, removed_at, removed_by)
			VALUES ({userId}, {roleId}, {removedAt}, {version}, FALSE, {removedAt}, {removedBy})
			ON CONFLICT (user_id, role_id) DO UPDATE
			SET last_version = EXCLUDED.last_version,
			    is_active = FALSE,
			    removed_at = EXCLUDED.removed_at,
			    removed_by = EXCLUDED.removed_by
			WHERE user_roles.last_version < EXCLUDED.last_version
		""", cancellationToken: ct);
	}

	public static Task<List<Guid>> RevokeUserSessionAsync(
		this DbContext context,
		Guid sessionId,
		DateTimeOffset revokedAt,
		CancellationToken ct = default)
	{
		return context.Database.SqlQuery<Guid>($"""
			UPDATE user_sessions SET revoked_at = {revokedAt}
			WHERE id = {sessionId} AND revoked_at IS NULL
			RETURNING id
		""").ToListAsync(cancellationToken: ct);
	}

	public static Task<List<Guid>> RevokeAllUserSessionsExceptAsync(
		this DbContext context,
		Guid userId,
		Guid exceptSessionId,
		DateTimeOffset revokedAt,
		CancellationToken ct = default)
	{
		return context.Database.SqlQuery<Guid>($"""
			UPDATE user_sessions SET revoked_at = {revokedAt}
			WHERE user_id = {userId} AND id != {exceptSessionId} AND revoked_at IS NULL
			RETURNING id
		""").ToListAsync(cancellationToken: ct);
	}

	public static Task<List<Guid>> RevokeAllUserSessionsAsync(
		this DbContext context,
		Guid userId,
		DateTimeOffset revokedAt,
		CancellationToken ct = default)
	{
		return context.Database.SqlQuery<Guid>($"""
			UPDATE user_sessions SET revoked_at = {revokedAt}
			WHERE user_id = {userId} AND revoked_at IS NULL
			RETURNING id
		""").ToListAsync(cancellationToken: ct);
	}

	public static Task<List<Guid>> SupersedeUserSessionAsync(
		this DbContext context,
		Guid sessionId,
		Guid successorSessionId,
		DateTimeOffset revokedAt,
		CancellationToken ct = default)
	{
		return context.Database.SqlQuery<Guid>($"""
			UPDATE user_sessions
			SET revoked_at = {revokedAt}, superseded_by_session_id = {successorSessionId}
			WHERE id = {sessionId} AND revoked_at IS NULL
			RETURNING id
		""").ToListAsync(cancellationToken: ct);
	}
}
