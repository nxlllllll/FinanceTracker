using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.ReadModels.Category;

public sealed record CategoryReadModel(
	Guid Id,
	Guid UserId,
	Guid? ParentId,
	Name Name,
	CategoryType Type,
	bool IsArchived,
	DateTimeOffset CreatedAt
) : IReadModel;
