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

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionCurrency;

public sealed class ChangeRecurringTransactionCurrencyHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<ChangeRecurringTransactionCurrencyHandler> logger
) : IAuthorizedHandler<ChangeRecurringTransactionCurrencyCommand, Core.Domains.RecurringTransaction.RecurringTransaction, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeRecurringTransactionCurrencyCommand command,
		Core.Domains.RecurringTransaction.RecurringTransaction entity,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = entity.ChangeCurrency(currency: command.Currency);
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);
		
		await recurringTransactionWriteRepository.ChangeCurrencyAsync(
			recurringTransactionId: command.RecurringTransactionId,
			expectedVersion: entity.RowVersion,
			currency: command.Currency,
			ct: ct
		);

		try
		{
			await publisher.Publish(notification: new RecurringTransactionCurrencyChangedNotification(
				RecurringTransactionId: entity.Id,
				UserId: entity.UserId,
				NewCurrency: command.Currency,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish RecurringTransactionCurrencyChangedNotification for recurring transaction {entity.Id} after successful commit.");
		}

		return Result<Guid, DomainException>.Success(value: entity.Id);
	}
}