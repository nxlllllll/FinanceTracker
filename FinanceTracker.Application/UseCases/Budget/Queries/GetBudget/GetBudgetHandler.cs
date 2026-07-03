using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Queries.GetBudget;

public sealed class GetBudgetHandler(
	IBudgetReadRepository budgetReadRepository
) : IRequestHandler<GetBudgetQuery, Result<BudgetReadModel, DomainException>>
{
	public async Task<Result<BudgetReadModel, DomainException>> Handle(
		GetBudgetQuery query,
		CancellationToken ct = default)
	{
		BudgetReadModel? model = await budgetReadRepository.GetByIdAsync(
			budgetId: query.BudgetId,
			userId: query.UserId,
			ct: ct
		);

		if (model is null)
			return Result<BudgetReadModel, DomainException>.Failure(error: new NotFoundException(message: "Budget not found.", id: query.BudgetId));

		return Result<BudgetReadModel, DomainException>.Success(value: model);
	}
}
