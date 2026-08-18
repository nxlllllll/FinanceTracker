using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Core.ReadModels.Budget;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Api.Endpoints.Budgets.Contracts;

/// <summary>
/// HTTP projection of <see cref="BudgetReadModel"/>.
/// </summary>
public sealed record BudgetResponse(
	Guid Id,
	Guid CategoryId,
	Money Amount,
	DateOnly From,
	DateOnly To,
	bool IsActive,
	DateTimeOffset CreatedAt
) : IResponseOf<BudgetReadModel, BudgetResponse>
{
	public static BudgetResponse FromReadModel(BudgetReadModel readModel) => new BudgetResponse(
		Id: readModel.Id,
		CategoryId: readModel.CategoryId,
		Amount: readModel.Amount,
		From: readModel.From,
		To: readModel.To,
		IsActive: readModel.IsActive,
		CreatedAt: readModel.CreatedAt
	);
}

/// <summary>
/// HTTP projection of <see cref="BudgetProgress"/>.
/// </summary>
public sealed record BudgetProgressResponse(
	Guid BudgetId,
	decimal Spent,
	decimal Remaining,
	decimal Percentage,
	DateTimeOffset UpdatedAt
) : IResponseOf<BudgetProgress, BudgetProgressResponse>
{
	public static BudgetProgressResponse FromReadModel(BudgetProgress readModel) => new BudgetProgressResponse(
		BudgetId: readModel.BudgetId,
		Spent: readModel.Spent,
		Remaining: readModel.Remaining,
		Percentage: readModel.Percentage,
		UpdatedAt: readModel.UpdatedAt
	);
}
