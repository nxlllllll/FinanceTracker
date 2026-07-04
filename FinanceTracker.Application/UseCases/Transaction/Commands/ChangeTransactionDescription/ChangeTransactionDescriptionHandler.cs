using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transaction.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionDescription;

public sealed class ChangeTransactionDescriptionHandler(
	ITransactionWriteRepository transactionWriteRepository,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<ChangeTransactionDescriptionHandler> logger
) : IAuthorizedHandler<ChangeTransactionDescriptionCommand, Core.Domains.Transaction.Transaction, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ChangeTransactionDescriptionCommand command,
		Core.Domains.Transaction.Transaction accounts,
		CancellationToken ct = default)
	{
		if (accounts.Description == command.Description)
			return Result<Guid, AppException>.Success(value: accounts.Id);

		string? oldDescription = accounts.Description;

		Result<Unit, DomainException> result = accounts.ChangeDescription(description: command.Description);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		await transactionWriteRepository.ChangeDescriptionAsync(
			transactionId: command.TransactionId,
			expectedVersion: accounts.RowVersion,
			description: command.Description,
			ct: ct
		);

		try
		{
			await publisher.Publish(notification: new TransactionDescriptionChangedNotification(
				TransactionId: accounts.Id,
				UserId: accounts.UserId,
				OldDescription: oldDescription,
				NewDescription: command.Description,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish TransactionDescriptionChangedNotification for transaction {accounts.Id} after successful commit.");
		}

		return Result<Guid, AppException>.Success(value: accounts.Id);
	}
}
