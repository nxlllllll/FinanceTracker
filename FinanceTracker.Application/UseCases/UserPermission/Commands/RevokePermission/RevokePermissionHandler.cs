using FinanceTracker.Application.Services.Permissions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.UserPermission.Commands.RevokePermission;

public sealed class RevokePermissionHandler(
	IUserPermissionService userPermissionService,
	IRootAuthority rootAuthority
) : IRequestHandler<RevokePermissionCommand, Result<Unit, AppException>>
{
	public async Task<Result<Unit, AppException>> Handle(
		RevokePermissionCommand command,
		CancellationToken ct = default)
	{
		if (command.TargetUserId == command.RevokedBy && !await rootAuthority.IsRootAsync(userId: command.RevokedBy, ct: ct))
			return Result<Unit, AppException>.Failure(error: new SelfPermissionModificationException());

		return await userPermissionService.RevokeAsync(
			targetUserId: command.TargetUserId,
			revokedBy: command.RevokedBy,
			permissions: [command.Permission],
			ct: ct
		);
	}
}
