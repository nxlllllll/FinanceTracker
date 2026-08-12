using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Role.Commands.DeleteRole;
using FinanceTracker.Application.UseCases.Role.Commands.UpdateRolePermissions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Role;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Role.Authorization;

public sealed class RoleLoader(
	IRoleRepository roleRepository
) : IEntityLoader<DeleteRoleCommand, RoleDto, AppException>,
	IEntityLoader<UpdateRolePermissionsCommand, RoleDto, AppException>
{
	public async Task<Result<RoleDto, AppException>> LoadAsync(
		DeleteRoleCommand request,
		CancellationToken ct)
	{
		Result<RoleDto, AppException> role = await LoadAsync(roleId: request.RoleId, ct: ct);
		if (role.IsFailure)
			return role;

		if (role.Value!.SystemKey is not null)
			return Result<RoleDto, AppException>.Failure(error: new CannotDeleteSystemRoleException());

		return role;
	}

	public Task<Result<RoleDto, AppException>> LoadAsync(
		UpdateRolePermissionsCommand request,
		CancellationToken ct
	) => LoadAsync(roleId: request.RoleId, ct: ct);

	private async Task<Result<RoleDto, AppException>> LoadAsync(Guid roleId, CancellationToken ct)
	{
		RoleDto? role = await roleRepository.GetByIdAsync(roleId: roleId, ct: ct);

		if (role is null)
			return Result<RoleDto, AppException>.Failure(error: new NotFoundException(message: "Role not found.", id: roleId));

		return Result<RoleDto, AppException>.Success(value: role);
	}
}
