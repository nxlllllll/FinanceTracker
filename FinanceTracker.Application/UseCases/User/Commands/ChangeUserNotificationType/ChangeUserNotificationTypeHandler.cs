using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Application.UseCases.User.Commands.ChangeUserNotificationType;

public sealed class ChangeUserNotificationTypeHandler(
	IUserWriteRepository userWriteRepository,
	IUnitOfWork unitOfWork,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IAuthorizedHandler<ChangeUserNotificationTypeCommand, Core.Domains.User.User, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ChangeUserNotificationTypeCommand command,
		Core.Domains.User.User user,
		CancellationToken ct = default)
	{
		NotificationType? oldNotificationType = user.NotificationType;

		Result<Unit, DomainException> result = user.ChangeNotificationType(newNotificationType: command.NewNotificationType);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		if (user.NotificationType == oldNotificationType)
			return Result<Guid, AppException>.Success(value: user.Id);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () => await userWriteRepository.ChangeNotificationTypeAsync(
			userId: command.UserId,
			newNotificationType: command.NewNotificationType,
			expectedVersion: user.RowVersion,
			ct: ct
		), ct: ct);

		postCommitNotifications.Stage(notification: new UserNotificationTypeChangedNotification(
			UserId: user.Id,
			OldNotificationType: oldNotificationType,
			NewNotificationType: command.NewNotificationType,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
