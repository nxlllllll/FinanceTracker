using FinanceTracker.Application.Services.Permissions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.Services.Roles;

public sealed class UserRoleService(
	IRoleRepository roleRepository,
	IUserPermissionService userPermissionService,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider
) : IUserRoleService
{
	public async Task<Result<Unit, AppException>> AssignAsync(
		Guid userId,
		Guid roleId,
		Guid assignedBy,
		CancellationToken ct = default)
	{
		RoleDto? role = await roleRepository.GetByIdAsync(roleId: roleId, ct: ct);
		if (role is null)
			return Result<Unit, AppException>.Failure(error: new NotFoundException(message: "Role not found.", id: roleId));

		return await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await roleRepository.AssignToUserAsync(
				userId: userId,
				roleId: roleId,
				assignedAt: dateProvider.UtcNow,
				ct: ct
			);

			return await userPermissionService.GrantAsync(
				targetUserId: userId,
				grantedBy: assignedBy,
				permissions: [..role.Permissions],
				ct: ct
			);
		}, ct: ct);
	}

	public async Task<Result<Unit, AppException>> RemoveAsync(
		Guid userId,
		Guid roleId,
		Guid removedBy,
		CancellationToken ct = default)
	{
		RoleDto? role = await roleRepository.GetByIdAsync(roleId: roleId, ct: ct);
		if (role is null)
			return Result<Unit, AppException>.Failure(error: new NotFoundException(message: "Role not found.", id: roleId));

		if (role.SystemKey == SystemRole.Root)
		{
			int rootHolders = await roleRepository.CountMembersWithSystemKeyAsync(
				systemKey: SystemRole.Root,
				ct: ct
			);

			if (rootHolders <= 1)
				return Result<Unit, AppException>.Failure(error: new LastRootRoleException());
		}

		return await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await roleRepository.RemoveFromUserAsync(
				userId: userId,
				roleId: roleId,
				ct: ct
			);

			return await userPermissionService.RevokeAsync(
				targetUserId: userId,
				revokedBy: removedBy,
				permissions: [..role.Permissions],
				ct: ct
			);
		}, ct: ct);
	}
}
