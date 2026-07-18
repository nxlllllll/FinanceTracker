using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.UserPermission.Commands.RevokePermission;

public sealed record RevokePermissionCommand(
	Guid TargetUserId,
	Permission Permission,
	Guid RevokedBy
) : IRequest<Result<Unit, AppException>>;
