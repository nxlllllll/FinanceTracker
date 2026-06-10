using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionCurrency;

public sealed class ChangeRecurringTransactionCurrencyHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IPublisher publisher,
	IDateProvider dateProvider
) : IAuthorizedHandler<ChangeRecurringTransactionCurrencyCommand, Core.Domains.RecurringTransaction.RecurringTransaction, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeRecurringTransactionCurrencyCommand command,
		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = recurringTransaction.ChangeCurrency(currency: command.Currency);
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);
		
		await recurringTransactionWriteRepository.ChangeCurrencyAsync(
			recurringTransactionId: command.RecurringTransactionId,
			currency: command.Currency,
			ct: ct
		);
		
		await publisher.Publish(notification: new RecurringTransactionCurrencyChangedNotification(
			RecurringTransactionId: recurringTransaction.Id,
			UserId: recurringTransaction.UserId,
			NewCurrency: command.Currency,
			OccurredAt: dateProvider.UtcNow
		), cancellationToken: ct);
		
		return Result<Guid, DomainException>.Success(value: recurringTransaction.Id);
	}
}
