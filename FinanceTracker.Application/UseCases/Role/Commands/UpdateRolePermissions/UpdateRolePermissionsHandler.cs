using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Role.Commands.UpdateRolePermissions;

/// <summary>
/// Replaces a role's permission set.
/// </summary>
public sealed class UpdateRolePermissionsHandler(
	IRoleRepository roleRepository,
	IUnitOfWork unitOfWork
) : IAuthorizedHandler<UpdateRolePermissionsCommand, RoleDto, Unit, AppException>
{
	public async Task<Result<Unit, AppException>> HandleAsync(
		UpdateRolePermissionsCommand request,
		RoleDto role,
		CancellationToken ct = default)
	{
		if (request.NewPermissions.SetEquals(other: role.Permissions))
			return Result<Unit, AppException>.Success(value: Unit.Default);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () => await roleRepository.ReplacePermissionsAsync(
			roleId: request.RoleId,
			permissions: request.NewPermissions,
			ct: ct
		), ct: ct);

		return Result<Unit, AppException>.Success(value: Unit.Default);
	}
}
