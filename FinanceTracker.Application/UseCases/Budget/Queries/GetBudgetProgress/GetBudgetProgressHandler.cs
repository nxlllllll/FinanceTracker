using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Queries.GetBudgetProgress;

public sealed class GetBudgetProgressHandler(
	IBudgetProgressReadRepository budgetProgressReadRepository
) : IRequestHandler<GetBudgetProgressQuery, Result<BudgetProgress, AppException>>
{
	public async Task<Result<BudgetProgress, AppException>> Handle(
		GetBudgetProgressQuery query,
		CancellationToken ct = default)
	{
		BudgetProgress? model = await budgetProgressReadRepository.GetByBudgetIdAsync(
			budgetId: query.BudgetId,
			userId: query.UserId,
			ct: ct
		);

		if (model is null)
			return Result<BudgetProgress, AppException>.Failure(error: new NotFoundException(message: "Budget progress not found.", id: query.BudgetId));

		return Result<BudgetProgress, AppException>.Success(value: model);
	}
}
