using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Application.UseCases.Transaction.Utilities;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels.Category;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using RT = FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.CreateRecurringTransaction;

public sealed class CreateRecurringTransactionHandler(
	ICategoryReadRepository categoryReadRepository,
	IUserQueryRepository userQueryRepository,
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IUnitOfWork unitOfWork,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IAuthorizedHandler<CreateRecurringTransactionCommand, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		CreateRecurringTransactionCommand command,
		CancellationToken ct = default)
	{
		CategoryReadModel? category = await categoryReadRepository.GetByIdAsync(categoryId: command.CategoryId, userId: command.UserId, ct: ct);
		if (category is null)
			return Result<Guid, AppException>.Failure(error: new NotFoundException(message: "Category not found.", id: command.CategoryId));

		DomainException? directionError = CategoryDirectionValidator.Validate(category: category, direction: command.Direction);
		if (directionError is not null)
			return Result<Guid, AppException>.Failure(error: directionError);

		Result<Money, DomainException> moneyResult = Money.Positive(amount: command.Amount, currency: command.Currency);
		if (moneyResult.IsFailure)
			return Result<Guid, AppException>.Failure(error: moneyResult.Error!);

		TimeZoneId? timeZone = await userQueryRepository.GetTimeZoneAsync(userId: command.UserId, ct: ct);
		if (timeZone is null)
			return Result<Guid, AppException>.Failure(error: new NotFoundException(message: "User not found.", id: command.UserId));

		Result<RT, DomainException> rtResult = RT.Create(
			createdAt: dateProvider.UtcNow,
			userId: command.UserId,
			accountId: command.AccountId,
			categoryId: command.CategoryId,
			amount: moneyResult.Value,
			direction: command.Direction,
			dayOfMonth: command.DayOfMonth,
			description: command.Description,
			timeZone: timeZone.Value
		);
		if (rtResult.IsFailure)
			return Result<Guid, AppException>.Failure(error: rtResult.Error!);

		RT recurringTransaction = rtResult.Value!;

		await unitOfWork.ExecuteInTransactionAsync(
			operation: async () => await recurringTransactionWriteRepository.CreateAsync(recurringTransaction: recurringTransaction, ct: ct),
			ct: ct
		);

		postCommitNotifications.Stage(notification: new RecurringTransactionCreatedNotification(
			RecurringTransactionId: recurringTransaction.Id,
			UserId: recurringTransaction.UserId,
			AccountId: recurringTransaction.AccountId,
			CategoryId: recurringTransaction.CategoryId,
			Amount: recurringTransaction.Amount,
			Direction: recurringTransaction.Direction,
			DayOfMonth: recurringTransaction.DayOfMonth,
			Description: recurringTransaction.Description,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: recurringTransaction.Id);
	}
}
