using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Application.UseCases.User.Commands.ChangeUserTimeZone;

public sealed class ChangeUserTimeZoneHandler(
	IUserWriteRepository userWriteRepository,
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IUnitOfWork unitOfWork,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IAuthorizedHandler<ChangeUserTimeZoneCommand, Core.Domains.User.User, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ChangeUserTimeZoneCommand command,
		Core.Domains.User.User user,
		CancellationToken ct = default)
	{
		TimeZoneId oldTimeZone = user.TimeZone;

		Result<Unit, DomainException> result = user.ChangeTimeZone(newTimeZone: command.NewTimeZone);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		if (user.TimeZone == oldTimeZone)
			return Result<Guid, AppException>.Success(value: user.Id);

		DateTimeOffset now = dateProvider.UtcNow;

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await userWriteRepository.ChangeTimeZoneAsync(
				userId: command.UserId,
				expectedVersion: user.RowVersion,
				newTimeZone: command.NewTimeZone,
				ct: ct
			);

			await recurringTransactionWriteRepository.RescheduleAllForUserAsync(
				userId: command.UserId,
				timeZone: command.NewTimeZone,
				ct: ct
			);
		}, ct: ct);

		postCommitNotifications.Stage(notification: new UserTimeZoneChangedNotification(
			UserId: user.Id,
			OldTimeZone: oldTimeZone,
			NewTimeZone: command.NewTimeZone,
			OccurredAt: now
		));

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
