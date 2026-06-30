using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.CreateRecurringTransaction;

public sealed class CreateRecurringTransactionHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IUnitOfWork unitOfWork,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<CreateRecurringTransactionHandler> logger
) : IAuthorizedHandler<CreateRecurringTransactionCommand, Core.Domains.Account.Account, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		CreateRecurringTransactionCommand command,
		Core.Domains.Account.Account accounts,
		CancellationToken ct = default)
	{
		Result<Money, DomainException> moneyResult = Money.Positive(amount: command.Amount, currency: command.Currency);
		if (moneyResult.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: moneyResult.Error!);
 
		Result<Core.Domains.RecurringTransaction.RecurringTransaction, DomainException> rtResult = Core.Domains.RecurringTransaction.RecurringTransaction.Create(
			createdAt: dateProvider.UtcNow,
			userId: command.UserId,
			accountId: command.AccountId,
			categoryId: command.CategoryId,
			amount: moneyResult.Value,
			direction: command.Direction,
			dayOfMonth: command.DayOfMonth,
			description: command.Description
		);
		if (rtResult.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: rtResult.Error!);
 
		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = rtResult.Value!;

		await unitOfWork.ExecuteInTransactionAsync(
			operation: async () => await recurringTransactionWriteRepository.CreateAsync(recurringTransaction: recurringTransaction, ct: ct),
			ct: ct
		);

		try
		{
			await publisher.Publish(notification: new RecurringTransactionCreatedNotification(
				RecurringTransactionId: recurringTransaction.Id,
				UserId: recurringTransaction.UserId,
				AccountId: recurringTransaction.AccountId,
				CategoryId: recurringTransaction.CategoryId,
				Amount: recurringTransaction.Amount,
				Direction: recurringTransaction.Direction,
				DayOfMonth: recurringTransaction.DayOfMonth,
				Description: recurringTransaction.Description,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish RecurringTransactionCreatedNotification for recurring transaction {recurringTransaction.Id} after successful commit.");
		}

		return Result<Guid, DomainException>.Success(value: recurringTransaction.Id);
	}
}