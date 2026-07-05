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

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ActivateRecurringTransaction;

public sealed class ActivateRecurringTransactionHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<ActivateRecurringTransactionHandler> logger
) : IAuthorizedHandler<ActivateRecurringTransactionCommand, Core.Domains.RecurringTransaction.RecurringTransaction, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ActivateRecurringTransactionCommand command,
		Core.Domains.RecurringTransaction.RecurringTransaction user,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = user.Activate();
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		await recurringTransactionWriteRepository.ActivateAsync(
			recurringTransactionId: command.RecurringTransactionId,
			expectedVersion: user.RowVersion,
			ct: ct
		);

		try
		{
			await publisher.Publish(notification: new RecurringTransactionActivatedNotification(
				RecurringTransactionId: user.Id,
				UserId: user.UserId,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish RecurringTransactionActivatedNotification for recurring transaction {user.Id} after successful commit.");
		}

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
