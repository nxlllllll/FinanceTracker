using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transaction.Notifications;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Transaction.Commands.ExcludeTransaction;

public sealed class ExcludeTransactionHandler(
	ITransactionWriteRepository transactionWriteRepository,
	ICategoryTotalWriteRepository categoryTotalWriteRepository,
	IBudgetProgressWriteRepository budgetProgressWriteRepository,
	IUnitOfWork unitOfWork,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<ExcludeTransactionHandler> logger
) : IAuthorizedHandler<ExcludeTransactionCommand, Core.Domains.Transaction.Transaction, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ExcludeTransactionCommand command,
		Core.Domains.Transaction.Transaction transaction,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = transaction.Exclude();
		if (result.IsFailure)
			return Result<Guid, DomainException>.Failure(error: result.Error!);
		
		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await transactionWriteRepository.ExcludeAsync(
				transactionId: command.TransactionId,
				userId: transaction.UserId,
				expectedVersion: transaction.RowVersion,
				ct: ct
			);
			
			if (transaction.Direction != DirectionType.Debit)
				return;

			await categoryTotalWriteRepository.SubtractAsync(
				userId: transaction.UserId,
				categoryId: transaction.CategoryId,
				currency: transaction.Amount.Currency,
				amount: transaction.Amount.Amount,
				occurredAt: transaction.OccurredAt,
				ct: ct
			);

			await budgetProgressWriteRepository.SubtractAsync(
				userId: transaction.UserId,
				categoryId: transaction.CategoryId,
				currencyCode: transaction.Amount.Currency,
				amount: transaction.Amount.Amount,
				occurredAt: transaction.OccurredAt,
				ct: ct
			);
		},
		onError: async exception => logger.ZLogError(exception: exception, message: $"Failed to exclude transaction {transaction.Id}."),
		ct: ct);
		
		await publisher.Publish(notification: new TransactionExcludedNotification(
			TransactionId: transaction.Id,
			UserId: transaction.UserId,
			OccurredAt: dateProvider.UtcNow
		), cancellationToken: ct);
		
		return Result<Guid, DomainException>.Success(value: transaction.Id);
	}
}