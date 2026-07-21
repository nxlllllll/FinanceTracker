using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Role.Queries.GetRole;

public sealed class GetRoleHandler(
	IRoleRepository roleRepository
) : IRequestHandler<GetRoleQuery, Result<RoleDto, AppException>>
{
	public async Task<Result<RoleDto, AppException>> Handle(
		GetRoleQuery query,
		CancellationToken ct = default)
	{
		RoleDto? role = await roleRepository.GetByIdAsync(roleId: query.RoleId, ct: ct);

		if (role is null)
			return Result<RoleDto, AppException>.Failure(error: new NotFoundException(message: "Role not found.", id: query.RoleId));

		return Result<RoleDto, AppException>.Success(value: role);
	}
}
