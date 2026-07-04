using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.User.Commands.ChangeUserBaseCurrency;

public sealed class ChangeUserBaseCurrencyHandler(
	IUserWriteRepository userWriteRepository,
	ICategoryTotalWriteRepository categoryTotalWriteRepository,
	IUnitOfWork unitOfWork,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<ChangeUserBaseCurrencyHandler> logger
) : IAuthorizedHandler<ChangeUserBaseCurrencyCommand, Core.Domains.User.User, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ChangeUserBaseCurrencyCommand command,
		Core.Domains.User.User user,
		CancellationToken ct = default)
	{
		Core.ValueObjects.Currency oldBaseCurrency = user.BaseCurrency;

		Result<Unit, DomainException> result = user.ChangeBaseCurrency(newBaseCurrency: command.NewBaseCurrency);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		if (user.BaseCurrency == oldBaseCurrency)
			return Result<Guid, AppException>.Success(value: user.Id);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await userWriteRepository.ChangeBaseCurrencyAsync(
				userId: command.UserId,
				expectedVersion: user.RowVersion,
				newBaseCurrencyCode: command.NewBaseCurrency,
				ct: ct
			);

			await categoryTotalWriteRepository.RecalculateAllForUserAsync(
				userId: command.UserId,
				baseCurrency: command.NewBaseCurrency,
				ct: ct
			);
		}, ct: ct);

		try
		{
			await publisher.Publish(notification: new UserBaseCurrencyChangedNotification(
				UserId: user.Id,
				OldBaseCurrency: oldBaseCurrency,
				NewBaseCurrency: command.NewBaseCurrency,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish UserBaseCurrencyChangedNotification for user {user.Id} after successful commit.");
		}

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
