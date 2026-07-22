using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Role.Notifications;
using FinanceTracker.Application.UseCases.UserPermission.Commands.GrantPermission;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Role.Commands.AssignRoleToUser;

public sealed class AssignRoleToUserHandler(
	IRoleRepository roleRepository,
	ISender sender,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider
) : IRequestHandler<AssignRoleToUserCommand, Result<Unit, AppException>>
{
	public async Task<Result<Unit, AppException>> Handle(
		AssignRoleToUserCommand command,
		CancellationToken ct = default)
	{
		RoleDto? role = await roleRepository.GetByIdAsync(roleId: command.RoleId, ct: ct);
		if (role is null)
			return Result<Unit, AppException>.Failure(error: new NotFoundException(message: "Role not found.", id: command.RoleId));

		await roleRepository.AssignToUserAsync(
			userId: command.UserId,
			roleId: command.RoleId,
			assignedAt: dateProvider.UtcNow,
			ct: ct
		);

		foreach (Permission permission in role.Permissions)
		{
			await sender.Send(request: new GrantPermissionCommand(
				TargetUserId: command.UserId,
				Permission: permission,
				GrantedBy: command.AssignedBy
			), cancellationToken: ct);
		}

		postCommitNotifications.Stage(notification: new RoleAssignedToUserNotification(
			UserId: command.UserId,
			RoleId: command.RoleId,
			AssignedBy: command.AssignedBy,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Unit, AppException>.Success(value: Unit.Default);
	}
}
