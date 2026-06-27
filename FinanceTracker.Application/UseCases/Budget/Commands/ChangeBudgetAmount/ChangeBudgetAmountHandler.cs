using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Budget.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Budget.Commands.ChangeBudgetAmount;

public sealed class ChangeBudgetAmountHandler(
	IBudgetWriteRepository budgetWriteRepository,
	IPublisher publisher,
	IDateProvider dateProvider
) : IAuthorizedHandler<ChangeBudgetAmountCommand, Core.Domains.Budget.Budget, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeBudgetAmountCommand command,
		Core.Domains.Budget.Budget entity,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = entity.ChangeAmount(amount: command.Amount);
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await budgetWriteRepository.ChangeAmountAsync(budgetId: entity.Id, expectedVersion: entity.RowVersion, amount: command.Amount, ct: ct);
		
		await publisher.Publish(notification: new BudgetAmountChangedNotification(
			BudgetId: entity.Id,
			UserId: entity.UserId,
			NewAmount: command.Amount,
			OccurredAt: dateProvider.UtcNow
		), cancellationToken: ct);
		
		return Result<Guid, DomainException>.Success(value: entity.Id);
	}
}
