using FinanceTracker.Application.Services.Permissions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Role.Commands.DeleteRole;

public sealed class DeleteRoleHandler(
	IRoleRepository roleRepository,
	IUserPermissionService userPermissionService,
	IUnitOfWork unitOfWork
) : IRequestHandler<DeleteRoleCommand, Result<Unit, AppException>>
{
	public async Task<Result<Unit, AppException>> Handle(
		DeleteRoleCommand command,
		CancellationToken ct = default)
	{
		RoleDto? role = await roleRepository.GetByIdAsync(roleId: command.RoleId, ct: ct);
		if (role is null)
			return Result<Unit, AppException>.Failure(error: new NotFoundException(message: "Role not found.", id: command.RoleId));

		if (role.SystemKey is not null)
			return Result<Unit, AppException>.Failure(error: new CannotDeleteSystemRoleException());

		IReadOnlyCollection<Permission> permissions = [.. role.Permissions];

		return await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			IReadOnlyList<Guid> memberUserIds = await roleRepository.GetMemberUserIdsAsync(roleId: command.RoleId, ct: ct);

			foreach (Guid userId in memberUserIds)
			{
				Result<Unit, AppException> revoked = await userPermissionService.RevokeAsync(
					targetUserId: userId,
					revokedBy: command.DeletedBy,
					permissions: permissions,
					ct: ct
				);
				if (revoked.IsFailure)
					return revoked;
			}

			await roleRepository.DeleteAsync(roleId: command.RoleId, ct: ct);

			return Result<Unit, AppException>.Success(value: Unit.Default);
		}, ct: ct);
	}
}
