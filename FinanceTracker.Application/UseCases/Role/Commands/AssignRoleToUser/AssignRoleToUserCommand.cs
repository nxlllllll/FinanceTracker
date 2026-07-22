using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Role.Commands.AssignRoleToUser;

public sealed record AssignRoleToUserCommand(
	Guid UserId,
	Guid RoleId,
	Guid AssignedBy
) : IRequest<Result<Unit, AppException>>;
