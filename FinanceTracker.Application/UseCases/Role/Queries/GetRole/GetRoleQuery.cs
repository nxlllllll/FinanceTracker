using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Role.Queries.GetRole;

public sealed record GetRoleQuery(Guid RoleId) : IRequest<Result<RoleDto, AppException>>;
