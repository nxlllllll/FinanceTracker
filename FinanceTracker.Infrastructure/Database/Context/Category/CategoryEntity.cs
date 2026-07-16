using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Infrastructure.Database.Context.Category;

public sealed class CategoryEntity
{
	public Guid Id { get; init; }
	public Guid UserId { get; init; }
	public Guid? ParentId { get; init; }
	public Name Name { get; init; }
	public CategoryType Type { get; init; }
	public bool IsArchived { get; init; }
	public int RowVersion { get; init; }
	public DateTimeOffset CreatedAt { get; init; }
}
