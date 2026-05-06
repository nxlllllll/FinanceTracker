using FinanceTracker.Application.UseCases.RecurringTransactions.Notifications;
using FinanceTracker.Application.UseCases.Transactions.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transactions.Services;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Projections;

public sealed class RecurringTransactionProjection(
	IAccountRepository accountRepository,
	ITransactionCreationService transactionCreationService,
	ILogger<RecurringTransactionProjection> logger
) : INotificationHandler<TransactionDataNotification>
{
	public async Task Handle(
		TransactionDataNotification notification,
		CancellationToken ct = default)
	{
		Account? account = await accountRepository.GetByIdAsync(accountId: notification.AccountId, ct: ct);
		if (account is null)
		{
			logger.ZLogError(message: $"Account {notification.AccountId} not found while processing recurring transaction for user {notification.UserId}.");
			throw new NotFoundException(message: "Account not found.", id: notification.AccountId);
		}

		await transactionCreationService.CreateAsync(
			command: new CreateTransactionCommand(
				AccountId: notification.AccountId,
				UserId: notification.UserId,
				CategoryId: notification.CategoryId,
				Amount: notification.Amount,
				Currency: notification.Currency,
				Direction: notification.Direction,
				Description: notification.Description,
				OccurredAt: notification.OccurredAt
			),
			account: account,
			ct: ct
		);
	}
}