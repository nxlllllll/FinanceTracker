using FinanceTracker.Application.UseCases.Budget.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Budget.Commands.CreateBudget;

public sealed class CreateBudgetHandler(
	IBudgetReadRepository budgetReadRepository,
	IBudgetWriteRepository budgetWriteRepository,
	IUnitOfWork unitOfWork,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<CreateBudgetHandler> logger
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
		bool hasOverlap = false;

		try
		{
			await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
			{
				hasOverlap = await budgetReadRepository.HasOverlappingAsync(
					userId: command.UserId,
					categoryId: command.CategoryId,
					from: command.From,
					to: command.To,
					ct: ct
				);

				if (hasOverlap)
					return;

				await budgetWriteRepository.CreateAsync(budget: budget, ct: ct);
			}, ct: ct);
		}
		catch (UniqueConstraintException)
		{
			hasOverlap = true;
		}

		if (hasOverlap)
			return Result<Guid, AppException>.Failure(error: new OverlappingBudgetException(message: "A budget for this category already exists in the specified period."));

		try
		{
			await publisher.Publish(notification: new BudgetCreatedNotification(
				BudgetId: budget.Id,
				UserId: budget.UserId,
				CategoryId: budget.CategoryId,
				Amount: budget.Amount,
				From: budget.From,
				To: budget.To,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish BudgetCreatedNotification for budget {budget.Id} after successful commit.");
		}

		return Result<Guid, AppException>.Success(value: budget.Id);
	}
}
