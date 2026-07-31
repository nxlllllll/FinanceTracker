using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Services.Permissions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Role.Commands.UpdateRolePermissions;

/// <summary>
/// Replaces a role's permission set and fans out the diff to every current member.
/// </summary>
public sealed class UpdateRolePermissionsHandler(
	IRoleRepository roleRepository,
	IUserPermissionService userPermissionService,
	IUnitOfWork unitOfWork
) : IAuthorizedHandler<UpdateRolePermissionsCommand, RoleDto, Unit, AppException>
{
	public async Task<Result<Unit, AppException>> HandleAsync(
		UpdateRolePermissionsCommand request,
		RoleDto role,
		CancellationToken ct = default)
	{
		IReadOnlyCollection<Permission> toGrant = [..request.NewPermissions.Except(second: role.Permissions)];
		IReadOnlyCollection<Permission> toRevoke = [..role.Permissions.Except(second: request.NewPermissions)];

		if (toGrant.Count == 0 && toRevoke.Count == 0)
			return Result<Unit, AppException>.Success(value: Unit.Default);

		return await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await roleRepository.ReplacePermissionsAsync(
				roleId: request.RoleId,
				permissions: request.NewPermissions,
				ct: ct
			);

			IReadOnlyList<Guid> memberUserIds = await roleRepository.GetMemberUserIdsAsync(roleId: request.RoleId, ct: ct);

			foreach (Guid userId in memberUserIds)
			{
				Result<Unit, AppException> granted = await userPermissionService.GrantAsync(
					targetUserId: userId,
					grantedBy: request.UpdatedBy,
					permissions: toGrant,
					ct: ct
				);
				if (granted.IsFailure)
					return granted;

				Result<Unit, AppException> revoked = await userPermissionService.RevokeAsync(
					targetUserId: userId,
					revokedBy: request.UpdatedBy,
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
