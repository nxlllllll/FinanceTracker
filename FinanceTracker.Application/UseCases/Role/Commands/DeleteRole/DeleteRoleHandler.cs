using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Services.Permissions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Role.Commands.DeleteRole;

public sealed class DeleteRoleHandler(
	IRoleRepository roleRepository,
	IUserPermissionService userPermissionService,
	IUnitOfWork unitOfWork
) : IAuthorizedHandler<DeleteRoleCommand, RoleDto, Unit, AppException>
{
	public async Task<Result<Unit, AppException>> HandleAsync(
		DeleteRoleCommand request,
		RoleDto role,
		CancellationToken ct = default)
	{
		IReadOnlyCollection<Permission> permissions = [..role.Permissions];

		return await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			IReadOnlyList<Guid> memberUserIds = await roleRepository.GetMemberUserIdsAsync(roleId: request.RoleId, ct: ct);

			foreach (Guid userId in memberUserIds)
			{
				Result<Unit, AppException> revoked = await userPermissionService.RevokeAsync(
					targetUserId: userId,
					revokedBy: request.DeletedBy,
					permissions: permissions,
					ct: ct
				);
				if (revoked.IsFailure)
					return revoked;
			}

			await roleRepository.DeleteAsync(roleId: request.RoleId, ct: ct);

			return Result<Unit, AppException>.Success(value: Unit.Default);
		}, ct: ct);
	}
}
