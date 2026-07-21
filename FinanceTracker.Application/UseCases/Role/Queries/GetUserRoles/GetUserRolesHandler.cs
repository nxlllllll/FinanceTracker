using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Role.Queries.GetUserRoles;

public sealed class GetUserRolesHandler(
	IRoleRepository roleRepository
) : IRequestHandler<GetUserRolesQuery, Result<IReadOnlyList<RoleDto>, AppException>>
{
	public async Task<Result<IReadOnlyList<RoleDto>, AppException>> Handle(
		GetUserRolesQuery query,
		CancellationToken ct = default
	) => Result<IReadOnlyList<RoleDto>, AppException>.Success(value: await roleRepository.GetByUserIdAsync(userId: query.UserId, ct: ct));
}
