using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.UserPermission.Commands.GrantPermission;

public sealed record GrantPermissionCommand(
	Guid TargetUserId,
	Permission Permission,
	Guid GrantedBy
) : IRequest<Result<Unit, AppException>>;

