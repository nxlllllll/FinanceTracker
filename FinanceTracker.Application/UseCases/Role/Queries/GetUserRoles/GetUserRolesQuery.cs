using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Role.Queries.GetUserRoles;

public sealed record GetUserRolesQuery(Guid UserId) : IRequest<Result<IReadOnlyList<RoleDto>, AppException>>;
