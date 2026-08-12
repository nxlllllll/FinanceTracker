using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using Unit = FinanceTracker.Core.Results.Unit;
using UserPermissionAggregate = FinanceTracker.Core.Domains.UserPermission.UserPermission;

namespace FinanceTracker.Application.Services.Permissions;

public sealed class UserPermissionService(
	IUserPermissionRepository userPermissionRepository,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider
) : IUserPermissionService
{
	public async Task<Result<Unit, AppException>> GrantAsync(
		Guid targetUserId,
		Guid grantedBy,
		IReadOnlyCollection<Permission> permissions,
		CancellationToken ct = default)
	{
		if (permissions.Count == 0)
			return Result<Unit, AppException>.Success(value: Unit.Default);

		DateTimeOffset now = dateProvider.UtcNow;
		UserPermissionAggregate? userPermission = await userPermissionRepository.GetByUserIdAsync(userId: targetUserId, ct: ct);

		if (userPermission is null)
		{
			Result<UserPermissionAggregate, DomainException> createResult = UserPermissionAggregate.Create(
				occurredAt: now,
				userId: targetUserId
			);
			if (createResult.IsFailure)
				return Result<Unit, AppException>.Failure(error: createResult.Error!);

			userPermission = createResult.Value!;
		}

		return await ApplyAndSaveAsync(
			userPermission: userPermission,
			permissions: permissions,
			apply: (aggregate, permission) => aggregate.Grant(occurredAt: now, grantedBy: grantedBy, permission: permission),
			ct: ct
		);
	}

	public async Task<Result<Unit, AppException>> RevokeAsync(
		Guid targetUserId,
		Guid revokedBy,
		IReadOnlyCollection<Permission> permissions,
		CancellationToken ct = default)
	{
		if (permissions.Count == 0)
			return Result<Unit, AppException>.Success(value: Unit.Default);

		DateTimeOffset now = dateProvider.UtcNow;
		UserPermissionAggregate? userPermission = await userPermissionRepository.GetByUserIdAsync(userId: targetUserId, ct: ct);

		if (userPermission is null)
			return Result<Unit, AppException>.Success(value: Unit.Default);

		return await ApplyAndSaveAsync(
			userPermission: userPermission,
			permissions: permissions,
			apply: (aggregate, permission) => aggregate.Revoke(occurredAt: now, revokedBy: revokedBy, permission: permission),
			ct: ct
		);
	}

	private async Task<Result<Unit, AppException>> ApplyAndSaveAsync(
		UserPermissionAggregate userPermission,
		IReadOnlyCollection<Permission> permissions,
		Func<UserPermissionAggregate, Permission, Result<Unit, DomainException>> apply,
		CancellationToken ct)
	{
		foreach (Permission permission in permissions)
		{
			Result<Unit, DomainException> result = apply(arg1: userPermission, arg2: permission);

			if (result.IsFailure)
				return Result<Unit, AppException>.Failure(error: result.Error!);
		}

		if (userPermission.Events.Count == 0)
			return Result<Unit, AppException>.Success(value: Unit.Default);

		await unitOfWork.ExecuteInTransactionAsync(
			operation: async () => await userPermissionRepository.SaveAsync(userPermission: userPermission, ct: ct),
			ct: ct
		);

		return Result<Unit, AppException>.Success(value: Unit.Default);
	}
}
