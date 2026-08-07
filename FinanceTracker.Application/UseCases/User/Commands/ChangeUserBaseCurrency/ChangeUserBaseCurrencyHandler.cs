using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.User.Commands.ChangeUserBaseCurrency;

public sealed class ChangeUserBaseCurrencyHandler(
	IUserWriteRepository userWriteRepository,
	IBaseCurrencyRecalculationWriteRepository recalculationWriteRepository,
	IUnitOfWork unitOfWork,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
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

		DateTimeOffset now = dateProvider.UtcNow;

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await userWriteRepository.ChangeBaseCurrencyAsync(
				userId: command.UserId,
				expectedVersion: user.RowVersion,
				newBaseCurrencyCode: command.NewBaseCurrency,
				ct: ct
			);

			await recalculationWriteRepository.RequestAsync(
				userId: command.UserId,
				targetCurrency: command.NewBaseCurrency,
				requestedAt: now,
				ct: ct
			);
		}, ct: ct);

		postCommitNotifications.Stage(notification: new UserBaseCurrencyChangedNotification(
			UserId: user.Id,
			OldBaseCurrency: oldBaseCurrency,
			NewBaseCurrency: command.NewBaseCurrency,
			OccurredAt: now
		));

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
