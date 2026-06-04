using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Commands.CreateBudget;

public sealed class CreateBudgetHandler(
	IBudgetReadRepository budgetReadRepository,
	IBudgetWriteRepository budgetWriteRepository,
	IUnitOfWork unitOfWork,
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

		Result<Core.Domains.Budget.Budget, DomainException> budgetResult = Core.Domains.Budget.Budget.Create(
			userId: command.UserId,
			categoryId: command.CategoryId,
			amount: moneyResult.Value,
			from: command.From,
			to: command.To,
			createdAt: dateProvider.UtcNow
		);
		if (budgetResult.IsFailure)
			return Result<Guid, DomainException>.Failure(error: budgetResult.Error!);

		Core.Domains.Budget.Budget budget = budgetResult.Value!;

		await unitOfWork.ExecuteInTransactionAsync(operation: async () => await budgetWriteRepository.CreateAsync(budget: budget, ct: ct), ct: ct);

		return Result<Guid, DomainException>.Success(value: budget.Id);
	}
}