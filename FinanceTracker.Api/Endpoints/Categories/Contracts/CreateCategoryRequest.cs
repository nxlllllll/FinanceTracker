using FinanceTracker.Core.Domains.Category;

namespace FinanceTracker.Api.Endpoints.Categories.Contracts;

public sealed record CreateCategoryRequest(
	string Name,
	CategoryType Type,
	Guid? ParentId
);
