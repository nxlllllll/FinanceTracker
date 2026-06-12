using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionAmount;

public sealed class ChangeRecurringTransactionAmountHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IPublisher publisher,
	IDateProvider dateProvider
) : IAuthorizedHandler<ChangeRecurringTransactionAmountCommand, Core.Domains.RecurringTransaction.RecurringTransaction, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeRecurringTransactionAmountCommand command,
		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = recurringTransaction.ChangeAmount(amount: command.Amount);
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await recurringTransactionWriteRepository.ChangeAmountAsync(
			recurringTransactionId: command.RecurringTransactionId,
			expectedVersion: recurringTransaction.RowVersion,
			amount: command.Amount,
			ct: ct
		);
		
		await publisher.Publish(notification: new RecurringTransactionAmountChangedNotification(
			RecurringTransactionId: recurringTransaction.Id,
			UserId: recurringTransaction.UserId,
			NewAmount: command.Amount,
			OccurredAt: dateProvider.UtcNow
		), cancellationToken: ct);
		
		return Result<Guid, DomainException>.Success(value: recurringTransaction.Id);
	}
}
