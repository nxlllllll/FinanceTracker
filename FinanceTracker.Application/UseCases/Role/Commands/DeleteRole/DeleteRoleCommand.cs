using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Role.Commands.DeleteRole;

public sealed record DeleteRoleCommand(
	Guid RoleId,
	Guid DeletedBy
) : IRequest<Result<Unit, AppException>>;
