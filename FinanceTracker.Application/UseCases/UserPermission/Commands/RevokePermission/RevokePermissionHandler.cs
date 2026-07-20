using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.UserPermission.Commands.RevokePermission;

public sealed class RevokePermissionHandler(
	IUserPermissionRepository userPermissionRepository,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider,
	IRootAuthority rootAuthority
) : IRequestHandler<RevokePermissionCommand, Result<Unit, AppException>>
{
	public async Task<Result<Unit, AppException>> Handle(
		RevokePermissionCommand command,
		CancellationToken ct = default)
	{
		if (command.TargetUserId == command.RevokedBy && !await rootAuthority.IsRootAsync(userId: command.RevokedBy, ct: ct))
			return Result<Unit, AppException>.Failure(error: new SelfPermissionModificationException());

		Core.Domains.UserPermission.UserPermission? userPermission = await userPermissionRepository.GetByUserIdAsync(userId: command.TargetUserId, ct: ct);

		if (userPermission is null)
			return Result<Unit, AppException>.Success(value: Unit.Default);

		Result<Unit, DomainException> revokeResult = userPermission.Revoke(
			occurredAt: dateProvider.UtcNow,
			revokedBy: command.RevokedBy,
			permission: command.Permission
		);
		if (revokeResult.IsFailure)
			return Result<Unit, AppException>.Failure(error: revokeResult.Error!);

		await unitOfWork.ExecuteInTransactionAsync(
			operation: async () => await userPermissionRepository.SaveAsync(userPermission: userPermission, ct: ct),
			ct: ct
		);

		return Result<Unit, AppException>.Success(value: Unit.Default);
	}
}
