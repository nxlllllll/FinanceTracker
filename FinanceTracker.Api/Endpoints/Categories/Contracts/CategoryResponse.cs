using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.ReadModels.Category;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Api.Endpoints.Categories.Contracts;

/// <summary>
/// HTTP projection of <see cref="CategoryReadModel"/>.
/// </summary>
public sealed record CategoryResponse(
	Guid Id,
	Guid? ParentId,
	Name Name,
	CategoryType Type,
	bool IsArchived,
	DateTimeOffset CreatedAt
) : IResponseOf<CategoryReadModel, CategoryResponse>
{
	public static CategoryResponse FromReadModel(CategoryReadModel readModel) => new CategoryResponse(
		Id: readModel.Id,
		ParentId: readModel.ParentId,
		Name: readModel.Name,
		Type: readModel.Type,
		IsArchived: readModel.IsArchived,
		CreatedAt: readModel.CreatedAt
	);
}
