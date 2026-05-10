using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FinanceTracker.Infrastructure.Database.Extensions;

public static class DbContextExtensions
{
    public static IQueryable<T> WithSkipLocked<T>(this DbContext context) where T : class
    {
        IEntityType entityType = context.Model.FindEntityType(type: typeof(T))!;
        string table = entityType.GetTableName()!;
        string? schema = entityType.GetSchema();

        string fullName = schema is null ? $"\"{table}\"" : $"\"{schema}\".\"{table}\"";

#pragma warning disable EF1002
        return context.Set<T>().FromSqlRaw(sql: $"SELECT * FROM {fullName} FOR UPDATE SKIP LOCKED");
#pragma warning restore EF1002
	}

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
}