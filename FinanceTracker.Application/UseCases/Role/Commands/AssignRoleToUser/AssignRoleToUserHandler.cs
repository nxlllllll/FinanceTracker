using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.Services.Roles;
using FinanceTracker.Application.UseCases.Role.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Role.Commands.AssignRoleToUser;

public sealed class AssignRoleToUserHandler(
	IUserRoleService userRoleService,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IRequestHandler<AssignRoleToUserCommand, Result<Unit, AppException>>
{
	public async Task<Result<Unit, AppException>> Handle(
		AssignRoleToUserCommand command,
		CancellationToken ct = default)
	{
		Result<Unit, AppException> result = await userRoleService.AssignAsync(
			userId: command.UserId,
			roleId: command.RoleId,
			assignedBy: command.AssignedBy,
			ct: ct
		);

		if (result.IsFailure)
			return result;

		postCommitNotifications.Stage(notification: new RoleAssignedToUserNotification(
			UserId: command.UserId,
			RoleId: command.RoleId,
			AssignedBy: command.AssignedBy,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Unit, AppException>.Success(value: Unit.Default);
	}
}
