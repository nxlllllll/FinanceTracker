using FinanceTracker.Infrastructure.Database.Context.Category;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Extensions;

public static class DbContextExtensions
{
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
}