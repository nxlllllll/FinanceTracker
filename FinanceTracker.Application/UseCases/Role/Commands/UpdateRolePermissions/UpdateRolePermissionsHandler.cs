using FinanceTracker.Application.Services.Permissions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Role.Commands.UpdateRolePermissions;

/// <summary>
/// Replaces a role's permission set and fans out the diff to every current member.
/// </summary>
public sealed class UpdateRolePermissionsHandler(
	IRoleRepository roleRepository,
	IUserPermissionService userPermissionService,
	IUnitOfWork unitOfWork
) : IRequestHandler<UpdateRolePermissionsCommand, Result<Unit, AppException>>
{
	public async Task<Result<Unit, AppException>> Handle(
		UpdateRolePermissionsCommand command,
		CancellationToken ct = default)
	{
		RoleDto? role = await roleRepository.GetByIdAsync(roleId: command.RoleId, ct: ct);
		if (role is null)
			return Result<Unit, AppException>.Failure(error: new NotFoundException(message: "Role not found.", id: command.RoleId));

		IReadOnlyCollection<Permission> toGrant = [..command.NewPermissions.Except(second: role.Permissions)];
		IReadOnlyCollection<Permission> toRevoke = [..role.Permissions.Except(second: command.NewPermissions)];

		if (toGrant.Count == 0 && toRevoke.Count == 0)
			return Result<Unit, AppException>.Success(value: Unit.Default);

		return await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await roleRepository.ReplacePermissionsAsync(
				roleId: command.RoleId,
				permissions: command.NewPermissions,
				ct: ct
			);

			IReadOnlyList<Guid> memberUserIds = await roleRepository.GetMemberUserIdsAsync(roleId: command.RoleId, ct: ct);

			foreach (Guid userId in memberUserIds)
			{
				Result<Unit, AppException> granted = await userPermissionService.GrantAsync(
					targetUserId: userId,
					grantedBy: command.UpdatedBy,
					permissions: toGrant,
					ct: ct
				);
				if (granted.IsFailure)
					return granted;

				Result<Unit, AppException> revoked = await userPermissionService.RevokeAsync(
					targetUserId: userId,
					revokedBy: command.UpdatedBy,
					permissions: toRevoke,
					ct: ct
				);
				if (revoked.IsFailure)
					return revoked;
			}

			return Result<Unit, AppException>.Success(value: Unit.Default);
		}, ct: ct);
	}
}
