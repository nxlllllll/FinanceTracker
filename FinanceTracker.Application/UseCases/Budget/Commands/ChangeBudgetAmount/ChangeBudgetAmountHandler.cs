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

namespace FinanceTracker.Application.UseCases.Budget.Commands.ChangeBudgetAmount;

public sealed class ChangeBudgetAmountHandler(
	IBudgetWriteRepository budgetWriteRepository,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<ChangeBudgetAmountHandler> logger
) : IAuthorizedHandler<ChangeBudgetAmountCommand, Core.Domains.Budget.Budget, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ChangeBudgetAmountCommand command,
		Core.Domains.Budget.Budget entity,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = entity.ChangeAmount(amount: command.Amount);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		await budgetWriteRepository.ChangeAmountAsync(budgetId: entity.Id, expectedVersion: entity.RowVersion, amount: command.Amount, ct: ct);

		try
		{
			await publisher.Publish(notification: new BudgetAmountChangedNotification(
				BudgetId: entity.Id,
				UserId: entity.UserId,
				NewAmount: command.Amount,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish BudgetAmountChangedNotification for budget {entity.Id} after successful commit.");
		}

		return Result<Guid, AppException>.Success(value: entity.Id);
	}
}
