using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Budget;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Queries.GetBudget;

public sealed class GetBudgetHandler(
	IBudgetReadRepository budgetReadRepository
) : IRequestHandler<GetBudgetQuery, Result<BudgetReadModel, AppException>>
{
	public async Task<Result<BudgetReadModel, AppException>> Handle(
		GetBudgetQuery query,
		CancellationToken ct = default)
	{
		BudgetReadModel? model = await budgetReadRepository.GetByIdAsync(
			budgetId: query.BudgetId,
			userId: query.UserId,
			ct: ct
		);

		if (model is null)
			return Result<BudgetReadModel, AppException>.Failure(error: new NotFoundException(message: "Budget not found.", id: query.BudgetId));

		return Result<BudgetReadModel, AppException>.Success(value: model);
	}
}
