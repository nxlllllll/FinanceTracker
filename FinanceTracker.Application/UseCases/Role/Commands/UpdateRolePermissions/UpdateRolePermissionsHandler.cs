using FinanceTracker.Application.UseCases.UserPermission.Commands.GrantPermission;
using FinanceTracker.Application.UseCases.UserPermission.Commands.RevokePermission;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Role.Commands.UpdateRolePermissions;

/// <summary>
/// Replaces a role's permission set and fans out the diff to every current member
/// </summary>
public sealed class UpdateRolePermissionsHandler(
	IRoleRepository roleRepository,
	ISender sender
) : IRequestHandler<UpdateRolePermissionsCommand, Result<Unit, AppException>>
{
	public async Task<Result<Unit, AppException>> Handle(
		UpdateRolePermissionsCommand command,
		CancellationToken ct = default)
	{
		RoleDto? role = await roleRepository.GetByIdAsync(roleId: command.RoleId, ct: ct);
		if (role is null)
			return Result<Unit, AppException>.Failure(error: new NotFoundException(message: "Role not found.", id: command.RoleId));

		IReadOnlySet<Permission> toGrant = command.NewPermissions.Except(second: role.Permissions).ToHashSet();
		IReadOnlySet<Permission> toRevoke = role.Permissions.Except(second: command.NewPermissions).ToHashSet();

		await roleRepository.ReplacePermissionsAsync(
			roleId: command.RoleId,
			permissions: command.NewPermissions,
			ct: ct
		);

		IReadOnlyList<Guid> memberUserIds = await roleRepository.GetMemberUserIdsAsync(roleId: command.RoleId, ct: ct);

		foreach (Guid userId in memberUserIds)
		{
			foreach (Permission permission in toGrant)
			{
				await sender.Send(request: new GrantPermissionCommand(
					TargetUserId: userId,
					Permission: permission,
					GrantedBy: command.UpdatedBy
				), cancellationToken: ct);
			}

			foreach (Permission permission in toRevoke)
			{
				await sender.Send(request: new RevokePermissionCommand(
					TargetUserId: userId,
					Permission: permission,
					RevokedBy: command.UpdatedBy
				), cancellationToken: ct);
			}
		}

		return Result<Unit, AppException>.Success(value: Unit.Default);
	}
}
