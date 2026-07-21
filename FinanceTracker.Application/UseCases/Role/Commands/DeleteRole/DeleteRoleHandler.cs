using FinanceTracker.Application.UseCases.UserPermission.Commands.RevokePermission;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Role.Commands.DeleteRole;

/// <summary>
/// Deletes a custom (non-system) role: revokes its permissions from every current member first
/// (raising the usual audit events), then removes the role and its remaining relational rows.
/// </summary>
public sealed class DeleteRoleHandler(
	IRoleRepository roleRepository,
	ISender sender
) : IRequestHandler<DeleteRoleCommand, Result<Unit, AppException>>
{
	public async Task<Result<Unit, AppException>> Handle(DeleteRoleCommand command, CancellationToken ct = default)
	{
		RoleDto? role = await roleRepository.GetByIdAsync(roleId: command.RoleId, ct: ct);
		if (role is null)
			return Result<Unit, AppException>.Failure(error: new NotFoundException(message: "Role not found.", id: command.RoleId));

		if (role.SystemKey is not null)
			return Result<Unit, AppException>.Failure(error: new CannotDeleteSystemRoleException());

		IReadOnlyList<Guid> memberUserIds = await roleRepository.GetMemberUserIdsAsync(roleId: command.RoleId, ct: ct);

		foreach (Guid userId in memberUserIds)
		{
			foreach (Permission permission in role.Permissions)
			{
				await sender.Send(request: new RevokePermissionCommand(
					TargetUserId: userId,
					Permission: permission,
					RevokedBy: command.DeletedBy
				), cancellationToken: ct);
			}
		}

		await roleRepository.DeleteAsync(roleId: command.RoleId, ct: ct);

		return Result<Unit, AppException>.Success(value: Unit.Default);
	}
}
