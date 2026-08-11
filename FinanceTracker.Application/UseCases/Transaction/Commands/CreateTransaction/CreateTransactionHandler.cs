using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.Services.Transactions;
using FinanceTracker.Application.UseCases.Transaction.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;

public sealed class CreateTransactionHandler(
	ITransactionCreationService transactionCreationService,
	IPostCommitNotifications postCommitNotifications
) : IAuthorizedHandler<CreateTransactionCommand, Core.Domains.Account.Account, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		CreateTransactionCommand command,
		Core.Domains.Account.Account account,
		CancellationToken ct = default)
	{
		Result<Core.Domains.Transaction.Transaction, DomainException> result = await transactionCreationService.CreateAsync(
			command: command,
			account: account,
			ct: ct
		);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		Core.Domains.Transaction.Transaction transaction = result.Value!;

		postCommitNotifications.Stage(notification: new TransactionCreatedNotification(
			TransactionId: transaction.Id,
			AccountId: transaction.AccountId,
			UserId: transaction.UserId,
			CategoryId: transaction.CategoryId,
			Amount: transaction.Amount,
			Direction: transaction.Direction,
			ExchangeRate: transaction.ExchangeRate,
			RateStatus: transaction.RateStatus,
			Description: transaction.Description,
			OccurredAt: transaction.OccurredAt
		));

		return Result<Guid, AppException>.Success(value: transaction.Id);
	}
}
