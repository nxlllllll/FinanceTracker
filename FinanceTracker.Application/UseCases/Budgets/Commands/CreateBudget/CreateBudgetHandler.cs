using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budgets.Commands.CreateBudget;

public sealed class CreateBudgetHandler(
	IBudgetReadRepository budgetReadRepository,
	IBudgetWriteRepository budgetWriteRepository,
	IDateProvider dateProvider
) : IRequestHandler<CreateBudgetCommand, Result<Guid, DomainException>>
{
	public async Task<Result<Guid, DomainException>> Handle(
		CreateBudgetCommand command,
		CancellationToken ct = default)
	{
		Result<Money, DomainException> moneyResult = Money.Positive(amount: command.Amount, currency: command.Currency);
		if (moneyResult.IsFailure)
			return Result<Guid, DomainException>.Failure(error: moneyResult.Error!);

		bool hasOverlap = await budgetReadRepository.HasOverlappingAsync(
			userId: command.UserId,
			categoryId: command.CategoryId,
			from: command.From,
			to: command.To,
			ct: ct
		);

		if (hasOverlap)
			return Result<Guid, DomainException>.Failure(error: new OverlappingBudgetException(message: "A budget for this category already exists in the specified period."));

		Result<Budget, DomainException> budgetResult = Budget.Create(
			createdAt: dateProvider.UtcNow,
			userId: command.UserId,
			categoryId: command.CategoryId,
			amount: moneyResult.Value,
			from: command.From,
			to: command.To
		);
		if (budgetResult.IsFailure)
			return Result<Guid, DomainException>.Failure(error: budgetResult.Error!);

		Budget budget = budgetResult.Value!;
		await budgetWriteRepository.CreateAsync(budget: budget, ct: ct);

		return Result<Guid, DomainException>.Success(value: budget.Id);
	}
}
