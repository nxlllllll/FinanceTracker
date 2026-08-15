using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Budget.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Budget;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Data;
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
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IRequestHandler<CreateBudgetCommand, Result<Guid, AppException>>
{
	public async Task<Result<Guid, AppException>> Handle(
		CreateBudgetCommand command,
		CancellationToken ct = default)
	{
		Result<Money, DomainException> moneyResult = Money.Positive(amount: command.Amount, currency: command.Currency);
		if (moneyResult.IsFailure)
			return Result<Guid, AppException>.Failure(error: moneyResult.Error!);

		Result<Core.Domains.Budget.Budget, DomainException> budgetResult = Core.Domains.Budget.Budget.Create(
			userId: command.UserId,
			categoryId: command.CategoryId,
			amount: moneyResult.Value,
			from: command.From,
			to: command.To,
			createdAt: dateProvider.UtcNow
		);
		if (budgetResult.IsFailure)
			return Result<Guid, AppException>.Failure(error: budgetResult.Error!);

		Core.Domains.Budget.Budget budget = budgetResult.Value!;
		bool hasOverlap;

		try
		{
			hasOverlap = await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
			{
				bool overlap = await budgetReadRepository.HasOverlappingAsync(
					userId: command.UserId,
					categoryId: command.CategoryId,
					from: command.From,
					to: command.To,
					ct: ct
				);

				if (overlap)
					return true;

				await budgetWriteRepository.CreateAsync(budget: budget, ct: ct);

				return false;
			}, ct: ct);
		}
		catch (UniqueConstraintException)
		{
			hasOverlap = true;
		}

		if (hasOverlap)
			return Result<Guid, AppException>.Failure(error: new OverlappingBudgetException(message: "A budget for this category already exists in the specified period."));

		postCommitNotifications.Stage(notification: new BudgetCreatedNotification(
			BudgetId: budget.Id,
			UserId: budget.UserId,
			CategoryId: budget.CategoryId,
			Amount: budget.Amount,
			From: budget.From,
			To: budget.To,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: budget.Id);
	}
}
