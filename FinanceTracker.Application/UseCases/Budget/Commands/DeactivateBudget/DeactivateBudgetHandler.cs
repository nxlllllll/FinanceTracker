using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Budget.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Budget.Commands.DeactivateBudget;

public sealed class DeactivateBudgetHandler(
	IBudgetWriteRepository budgetWriteRepository,
	IPublisher publisher,
	IDateProvider dateProvider
) : IAuthorizedHandler<DeactivateBudgetCommand, Core.Domains.Budget.Budget, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		DeactivateBudgetCommand command,
		Core.Domains.Budget.Budget budget,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = budget.Deactivate();
		if (result.IsFailure)
			return Result<Guid, DomainException>.Failure(error: result.Error!);
		
		await budgetWriteRepository.DeactivateAsync(budgetId: budget.Id, ct: ct);
		
		await publisher.Publish(notification: new BudgetDeactivatedNotification(
			BudgetId: budget.Id,
			UserId: budget.UserId,
			OccurredAt: dateProvider.UtcNow
		), cancellationToken: ct);
		
		return Result<Guid, DomainException>.Success(value: budget.Id);
	}
}