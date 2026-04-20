using FinanceTracker.Core.Domains.Category;

namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class CategoryEntity
{
	public Guid Id { get; init; }
	public Guid UserId { get; init; }
	public Guid? ParentId { get; init; }
	public string Name { get; set; } = String.Empty;
	public CategoryType Type { get; init; }
	public bool IsArchived { get; set; }
	public DateTime CreatedAt { get; init; }
}