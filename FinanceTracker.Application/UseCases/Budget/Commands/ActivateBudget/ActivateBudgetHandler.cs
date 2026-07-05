using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Budget.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Budget.Commands.ActivateBudget;

public sealed class ActivateBudgetHandler(
	IBudgetReadRepository budgetReadRepository,
	IBudgetWriteRepository budgetWriteRepository,
	IUnitOfWork unitOfWork,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<ActivateBudgetHandler> logger
) : IAuthorizedHandler<ActivateBudgetCommand, Core.Domains.Budget.Budget, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ActivateBudgetCommand command,
		Core.Domains.Budget.Budget user,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = user.Activate();
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		bool hasOverlap;

		try
		{
			hasOverlap = await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
			{
				bool overlap = await budgetReadRepository.HasOverlappingAsync(
					userId: command.UserId,
					categoryId: user.CategoryId,
					from: user.From,
					to: user.To,
					excludeBudgetId: user.Id,
					ct: ct
				);

				if (overlap)
					return true;

				await budgetWriteRepository.ActivateAsync(budgetId: user.Id, expectedVersion: user.RowVersion, ct: ct);

				return false;
			}, ct: ct);
		}
		catch (UniqueConstraintException)
		{
			hasOverlap = true;
		}

		if (hasOverlap)
		{
			return Result<Guid, AppException>.Failure(error: new OverlappingBudgetException(
				message: "Cannot activate: a budget for this category already exists in an overlapping period."
			));
		}

		try
		{
			await publisher.Publish(notification: new BudgetActivatedNotification(
				BudgetId: user.Id,
				UserId: user.UserId,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish BudgetActivatedNotification for budget {user.Id} after successful commit.");
		}

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
