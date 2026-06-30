using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ActivateRecurringTransaction;

public sealed class ActivateRecurringTransactionHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<ActivateRecurringTransactionHandler> logger
) : IAuthorizedHandler<ActivateRecurringTransactionCommand, Core.Domains.RecurringTransaction.RecurringTransaction, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ActivateRecurringTransactionCommand command,
		Core.Domains.RecurringTransaction.RecurringTransaction entity,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = entity.Activate();
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await recurringTransactionWriteRepository.ActivateAsync(
			recurringTransactionId: command.RecurringTransactionId,
			expectedVersion: entity.RowVersion,
			ct: ct
		);

		try
		{
			await publisher.Publish(notification: new RecurringTransactionActivatedNotification(
				RecurringTransactionId: entity.Id,
				UserId: entity.UserId,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish RecurringTransactionActivatedNotification for recurring transaction {entity.Id} after successful commit.");
		}

		return Result<Guid, DomainException>.Success(value: entity.Id);
	}
}