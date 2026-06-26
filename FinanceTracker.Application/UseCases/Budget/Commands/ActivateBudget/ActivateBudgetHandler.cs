using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Budget.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Budget.Commands.ActivateBudget;

public sealed class ActivateBudgetHandler(
	IBudgetReadRepository budgetReadRepository,
	IBudgetWriteRepository budgetWriteRepository,
	IUnitOfWork unitOfWork,
	IPublisher publisher,
	IDateProvider dateProvider
) : IAuthorizedHandler<ActivateBudgetCommand, Core.Domains.Budget.Budget, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ActivateBudgetCommand command,
		Core.Domains.Budget.Budget accounts,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = accounts.Activate();
		if (result.IsFailure)
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		bool hasOverlap;

		try
		{
			hasOverlap = await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
			{
				bool overlap = await budgetReadRepository.HasOverlappingAsync(
					userId: command.UserId,
					categoryId: accounts.CategoryId,
					from: accounts.From,
					to: accounts.To,
					excludeBudgetId: accounts.Id,
					ct: ct
				);

				if (overlap)
					return true;

				await budgetWriteRepository.ActivateAsync(budgetId: accounts.Id, expectedVersion: accounts.RowVersion, ct: ct);

				return false;
			}, ct: ct);
		}
		catch (UniqueConstraintException)
		{
			hasOverlap = true;
		}

		if (hasOverlap)
		{
			return Result<Guid, DomainException>.Failure(error: new OverlappingBudgetException(
				message: "Cannot activate: a budget for this category already exists in an overlapping period."
			));
		}

		await publisher.Publish(notification: new BudgetActivatedNotification(
			BudgetId: accounts.Id,
			UserId: accounts.UserId,
			OccurredAt: dateProvider.UtcNow
		), cancellationToken: ct);

		return Result<Guid, DomainException>.Success(value: accounts.Id);
	}
}