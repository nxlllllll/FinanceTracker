using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Transaction.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionDescription;

public sealed class ChangeTransactionDescriptionHandler(
	ITransactionWriteRepository transactionWriteRepository,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IAuthorizedHandler<ChangeTransactionDescriptionCommand, Core.Domains.Transaction.Transaction, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ChangeTransactionDescriptionCommand command,
		Core.Domains.Transaction.Transaction transaction,
		CancellationToken ct = default)
	{
		if (transaction.Description == command.Description)
			return Result<Guid, AppException>.Success(value: transaction.Id);

		string? oldDescription = transaction.Description;

		Result<bool, DomainException> result = transaction.ChangeDescription(description: command.Description);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		if (!result.Value)
			return Result<Guid, AppException>.Success(value: transaction.Id);

		await transactionWriteRepository.ChangeDescriptionAsync(
			transactionId: command.TransactionId,
			userId: transaction.UserId,
			expectedVersion: transaction.RowVersion,
			description: command.Description,
			ct: ct
		);

		postCommitNotifications.Stage(notification: new TransactionDescriptionChangedNotification(
			TransactionId: transaction.Id,
			UserId: transaction.UserId,
			OldDescription: oldDescription,
			NewDescription: command.Description,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: transaction.Id);
	}
}
