using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Budget.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Budget.Commands.DeactivateBudget;

public sealed class DeactivateBudgetHandler(
	IBudgetWriteRepository budgetWriteRepository,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<DeactivateBudgetHandler> logger
) : IAuthorizedHandler<DeactivateBudgetCommand, Core.Domains.Budget.Budget, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		DeactivateBudgetCommand command,
		Core.Domains.Budget.Budget entity,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = entity.Deactivate();
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		await budgetWriteRepository.DeactivateAsync(budgetId: entity.Id, expectedVersion: entity.RowVersion, ct: ct);

		try
		{
			await publisher.Publish(notification: new BudgetDeactivatedNotification(
				BudgetId: entity.Id,
				UserId: entity.UserId,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish BudgetDeactivatedNotification for budget {entity.Id} after successful commit.");
		}

		return Result<Guid, AppException>.Success(value: entity.Id);
	}
}
