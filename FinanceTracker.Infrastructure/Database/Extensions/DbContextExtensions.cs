using System.Linq.Expressions;
using System.Text.Json;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

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

    public static Task ChangePayloadAsync<TEntity, TPayload, TValue>(
        this DbContext context,
        Guid id,
        Expression<Func<TPayload, TValue>> property,
        TValue value,
        CancellationToken ct = default) where TEntity : class
    {
        string propertyName = ((MemberExpression)property.Body).Member.Name;
        string jsonKey = FinanceTrackerJsonOptions.Payload.PropertyNamingPolicy?.ConvertName(propertyName) ?? propertyName;

        IEntityType entityType = context.Model.FindEntityType(typeof(TEntity))!;
        string tableName = entityType.GetTableName()!;
        string jsonValue = JsonSerializer.Serialize(value: value, options: FinanceTrackerJsonOptions.Payload);

        string sql = "UPDATE \"" + tableName + "\" SET payload = jsonb_set(payload, '{{" + jsonKey + "}}', @p0::jsonb) WHERE id = @p1";

#pragma warning disable EF1002
        return context.Database.ExecuteSqlRawAsync(
            sql: sql,
            parameters: [
                new NpgsqlParameter(parameterName: "p0", value: jsonValue),
                new NpgsqlParameter(parameterName: "p1", value: id)
            ],
            cancellationToken: ct
        );
#pragma warning restore EF1002
    }
    
    public static Task InsertIdempotentCommandAsync(
        this DbContext context,
        Guid idempotencyKey,
        string commandType,
        string responseJson,
        DateTime createdAt,
        DateTime expiresAt,
        CancellationToken ct = default)
    {
        return context.Database.ExecuteSqlAsync(sql: $"""
            INSERT INTO idempotent_commands (idempotency_key, command_type, response_json, created_at, expires_at)
            VALUES ({idempotencyKey}, {commandType}, {responseJson}, {createdAt}, {expiresAt})
            ON CONFLICT (idempotency_key) DO NOTHING
            """, cancellationToken: ct);
    }
}