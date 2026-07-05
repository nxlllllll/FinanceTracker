using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Exceptions;
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
) : IAuthorizedHandler<ChangeRecurringTransactionCurrencyCommand, Core.Domains.RecurringTransaction.RecurringTransaction, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ChangeRecurringTransactionCurrencyCommand command,
		Core.Domains.RecurringTransaction.RecurringTransaction user,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = user.ChangeCurrency(currency: command.Currency);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		await recurringTransactionWriteRepository.ChangeCurrencyAsync(
			recurringTransactionId: command.RecurringTransactionId,
			expectedVersion: user.RowVersion,
			currency: command.Currency,
			ct: ct
		);

		try
		{
			await publisher.Publish(notification: new RecurringTransactionCurrencyChangedNotification(
				RecurringTransactionId: user.Id,
				UserId: user.UserId,
				NewCurrency: command.Currency,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish RecurringTransactionCurrencyChangedNotification for recurring transaction {user.Id} after successful commit.");
		}

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
