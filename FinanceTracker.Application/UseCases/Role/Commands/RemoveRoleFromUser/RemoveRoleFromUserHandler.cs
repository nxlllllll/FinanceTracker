using FinanceTracker.Application.UseCases.UserPermission.Commands.RevokePermission;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Role.Commands.RemoveRoleFromUser;

public sealed class RemoveRoleFromUserHandler(
	IRoleRepository roleRepository,
	ISender sender
) : IRequestHandler<RemoveRoleFromUserCommand, Result<Unit, AppException>>
{
	public async Task<Result<Unit, AppException>> Handle(
		RemoveRoleFromUserCommand command,
		CancellationToken ct = default)
	{
		RoleDto? role = await roleRepository.GetByIdAsync(roleId: command.RoleId, ct: ct);
		if (role is null)
			return Result<Unit, AppException>.Failure(error: new NotFoundException(message: "Role not found.", id: command.RoleId));

		if (role.SystemKey?.Equals(value: nameof(SystemRole.Root), comparisonType: StringComparison.InvariantCultureIgnoreCase) ?? false)
		{
			int rootHolders = await roleRepository.CountMembersWithSystemKeyAsync(
				systemKey: nameof(SystemRole.Root).ToLowerInvariant(),
				ct: ct
			);
			if (rootHolders <= 1)
				return Result<Unit, AppException>.Failure(error: new LastRootRoleException());
		}

		await roleRepository.RemoveFromUserAsync(userId: command.UserId, roleId: command.RoleId, ct: ct);

		foreach (Permission permission in role.Permissions)
		{
			await sender.Send(request: new RevokePermissionCommand(
				TargetUserId: command.UserId,
				Permission: permission,
				RevokedBy: command.RemovedBy
			), cancellationToken: ct);
		}

		return Result<Unit, AppException>.Success(value: Unit.Default);
	}
}
