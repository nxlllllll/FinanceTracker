using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Core.ReadModels.Category;

namespace FinanceTracker.Api.Endpoints.Categories.Contracts;

/// <summary>
/// One category's spend for one month, denominated in the user's base currency rather than in the
/// currency each transaction was recorded in — which is why it is a bare number and not a Money.
/// </summary>
public sealed record CategoryTotalItem(
	Guid CategoryId,
	DateOnly Period,
	decimal Total,
	int Count,
	DateTimeOffset? UpdatedAt
)
{
	public static CategoryTotalItem FromReadModel(CategoryTotal readModel) => new CategoryTotalItem(
		CategoryId: readModel.CategoryId,
		Period: readModel.Period,
		Total: readModel.Total,
		Count: readModel.Count,
		UpdatedAt: readModel.UpdatedAt
	);
}

/// <summary>
/// HTTP projection of <see cref="CategoryTotalView"/>.
/// </summary>
public sealed record CategoryTotalResponse(
	CategoryTotalItem? Total,
	bool RecalculationPending
) : IResponseOf<CategoryTotalView, CategoryTotalResponse>
{
	public static CategoryTotalResponse FromReadModel(CategoryTotalView readModel) => new CategoryTotalResponse(
		Total: readModel.Total is null ? null : CategoryTotalItem.FromReadModel(readModel: readModel.Total),
		RecalculationPending: readModel.RecalculationPending
	);
}

/// <summary>
/// HTTP projection of <see cref="CategoryTotalsView"/>
/// </summary>
public sealed record CategoryTotalsResponse(
	IReadOnlyList<CategoryTotalItem> Totals,
	bool RecalculationPending
) : IResponseOf<CategoryTotalsView, CategoryTotalsResponse>
{
	public static CategoryTotalsResponse FromReadModel(CategoryTotalsView readModel) => new CategoryTotalsResponse(
		Totals: [.. readModel.Totals.Select(selector: CategoryTotalItem.FromReadModel)],
		RecalculationPending: readModel.RecalculationPending
	);
}
