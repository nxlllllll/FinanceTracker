using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Role.Commands.RemoveRoleFromUser;

public sealed record RemoveRoleFromUserCommand(
	Guid UserId,
	Guid RoleId,
	Guid RemovedBy
) : IRequest<Result<Unit, AppException>>;
