using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Role.Queries.GetRoles;

public sealed record GetRolesQuery : IRequest<Result<IReadOnlyList<RoleDto>, AppException>>;
