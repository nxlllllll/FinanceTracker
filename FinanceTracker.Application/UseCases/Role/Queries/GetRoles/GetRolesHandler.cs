using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Role.Queries.GetRoles;

public sealed class GetRolesHandler(
	IRoleRepository roleRepository
) : IRequestHandler<GetRolesQuery, Result<IReadOnlyList<RoleDto>, AppException>>
{
	public async Task<Result<IReadOnlyList<RoleDto>, AppException>> Handle(
		GetRolesQuery query,
		CancellationToken ct = default
	) => Result<IReadOnlyList<RoleDto>, AppException>.Success(value: await roleRepository.GetAllAsync(ct: ct));
}
