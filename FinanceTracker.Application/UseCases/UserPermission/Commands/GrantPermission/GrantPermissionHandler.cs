using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.UserPermission.Commands.GrantPermission;

public sealed class GrantPermissionHandler(
	IUserPermissionRepository userPermissionRepository,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider,
	IRootAuthority rootAuthority
) : IRequestHandler<GrantPermissionCommand, Result<Unit, AppException>>
{
	public async Task<Result<Unit, AppException>> Handle(
		GrantPermissionCommand command,
		CancellationToken ct = default)
	{
		if (command.TargetUserId == command.GrantedBy && !await rootAuthority.IsRootAsync(userId: command.GrantedBy, ct: ct))
			return Result<Unit, AppException>.Failure(error: new SelfPermissionModificationException());

		Core.Domains.UserPermission.UserPermission? userPermission = await userPermissionRepository.GetByUserIdAsync(userId: command.TargetUserId, ct: ct);

		if (userPermission is null)
		{
			Result<Core.Domains.UserPermission.UserPermission, DomainException> createResult = Core.Domains.UserPermission.UserPermission.Create(
				occurredAt: dateProvider.UtcNow,
				userId: command.TargetUserId
			);
			if (createResult.IsFailure)
				return Result<Unit, AppException>.Failure(error: createResult.Error!);

			userPermission = createResult.Value!;
		}

		Result<Unit, DomainException> grantResult = userPermission.Grant(
			occurredAt: dateProvider.UtcNow,
			grantedBy: command.GrantedBy,
			permission: command.Permission
		);
		if (grantResult.IsFailure)
			return Result<Unit, AppException>.Failure(error: grantResult.Error!);

		await unitOfWork.ExecuteInTransactionAsync(
			operation: async () => await userPermissionRepository.SaveAsync(userPermission: userPermission, ct: ct),
			ct: ct
		);

		return Result<Unit, AppException>.Success(value: Unit.Default);
	}
}
