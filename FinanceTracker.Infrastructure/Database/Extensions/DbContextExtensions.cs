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
}