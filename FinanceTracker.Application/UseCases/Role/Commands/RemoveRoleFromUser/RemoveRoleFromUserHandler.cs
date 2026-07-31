using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.Services.Roles;
using FinanceTracker.Application.UseCases.Role.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Role.Commands.RemoveRoleFromUser;

public sealed class RemoveRoleFromUserHandler(
	IUserRoleService userRoleService,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IRequestHandler<RemoveRoleFromUserCommand, Result<Unit, AppException>>
{
	public async Task<Result<Unit, AppException>> Handle(
		RemoveRoleFromUserCommand command,
		CancellationToken ct = default)
	{
		Result<Unit, AppException> result = await userRoleService.RemoveAsync(
			userId: command.UserId,
			roleId: command.RoleId,
			removedBy: command.RemovedBy,
			ct: ct
		);

		if (result.IsFailure)
			return result;

		postCommitNotifications.Stage(notification: new RoleRemovedFromUserNotification(
			UserId: command.UserId,
			RoleId: command.RoleId,
			RemovedBy: command.RemovedBy,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Unit, AppException>.Success(value: Unit.Default);
	}
}
