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
		Core.Domains.Budget.Budget user,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = user.Deactivate();
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		await budgetWriteRepository.DeactivateAsync(budgetId: user.Id, expectedVersion: user.RowVersion, ct: ct);

		try
		{
			await publisher.Publish(notification: new BudgetDeactivatedNotification(
				BudgetId: user.Id,
				UserId: user.UserId,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish BudgetDeactivatedNotification for budget {user.Id} after successful commit.");
		}

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
