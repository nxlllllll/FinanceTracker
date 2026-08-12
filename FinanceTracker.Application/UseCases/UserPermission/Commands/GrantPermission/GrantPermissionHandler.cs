using FinanceTracker.Application.Services.Permissions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Permission;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.UserPermission.Commands.GrantPermission;

public sealed class GrantPermissionHandler(
	IUserPermissionService userPermissionService,
	IRootAuthority rootAuthority
) : IRequestHandler<GrantPermissionCommand, Result<Unit, AppException>>
{
	public async Task<Result<Unit, AppException>> Handle(
		GrantPermissionCommand command,
		CancellationToken ct = default)
	{
		if (command.TargetUserId == command.GrantedBy && !await rootAuthority.IsRootAsync(userId: command.GrantedBy, ct: ct))
			return Result<Unit, AppException>.Failure(error: new SelfPermissionModificationException());

		return await userPermissionService.GrantAsync(
			targetUserId: command.TargetUserId,
			grantedBy: command.GrantedBy,
			permissions: [command.Permission],
			ct: ct
		);
	}
}
