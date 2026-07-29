using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.Services.Permissions;
using FinanceTracker.Application.UseCases.Role.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Role.Commands.AssignRoleToUser;

public sealed class AssignRoleToUserHandler(
	IRoleRepository roleRepository,
	IUserPermissionService userPermissionService,
	IUnitOfWork unitOfWork,
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

		Result<Unit, AppException> result = await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await roleRepository.AssignToUserAsync(
				userId: command.UserId,
				roleId: command.RoleId,
				assignedAt: dateProvider.UtcNow,
				ct: ct
			);

			return await userPermissionService.GrantAsync(
				targetUserId: command.UserId,
				grantedBy: command.AssignedBy,
				permissions: [..role.Permissions],
				ct: ct
			);
		}, ct: ct);

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
