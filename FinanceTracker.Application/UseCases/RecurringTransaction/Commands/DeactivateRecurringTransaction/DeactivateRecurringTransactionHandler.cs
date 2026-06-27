using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.DeactivateRecurringTransaction;

public sealed class DeactivateRecurringTransactionHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IPublisher publisher,
	IDateProvider dateProvider
) : IAuthorizedHandler<DeactivateRecurringTransactionCommand, Core.Domains.RecurringTransaction.RecurringTransaction, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		DeactivateRecurringTransactionCommand command,
		Core.Domains.RecurringTransaction.RecurringTransaction entity,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = entity.Deactivate();
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await recurringTransactionWriteRepository.DeactivateAsync(
			recurringTransactionId: command.RecurringTransactionId,
			expectedVersion: entity.RowVersion,
			ct: ct
		);

		await publisher.Publish(notification: new RecurringTransactionDeactivatedNotification(
			RecurringTransactionId: entity.Id,
			UserId: entity.UserId,
			OccurredAt: dateProvider.UtcNow
		), cancellationToken: ct);
		
		return Result<Guid, DomainException>.Success(value: entity.Id);
	}
}
