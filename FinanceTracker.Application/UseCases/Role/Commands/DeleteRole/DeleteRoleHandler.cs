using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Role.Commands.DeleteRole;

/// <summary>Deletes a role that nobody belongs to.</summary>
public sealed class DeleteRoleHandler(
	IRoleRepository roleRepository
) : IAuthorizedHandler<DeleteRoleCommand, RoleDto, Unit, AppException>
{
	public async Task<Result<Unit, AppException>> HandleAsync(
		DeleteRoleCommand request,
		RoleDto role,
		CancellationToken ct = default)
	{
		IReadOnlyList<Guid> memberUserIds = await roleRepository.GetMemberUserIdsAsync(roleId: request.RoleId, ct: ct);

		if (memberUserIds.Any())
		{
			return Result<Unit, AppException>.Failure(error: new RoleHasMembersException(
				roleId: request.RoleId,
				memberCount: memberUserIds.Count
			));
		}

		await roleRepository.DeleteAsync(roleId: request.RoleId, ct: ct);

		return Result<Unit, AppException>.Success(value: Unit.Default);
	}
}
