using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Repositories.UserRole;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using Unit = FinanceTracker.Core.Results.Unit;
using UserRoleAggregate = FinanceTracker.Core.Domains.UserRole.UserRole;

namespace FinanceTracker.Application.Services.Roles;

public sealed class UserRoleService(
	IRoleRepository roleRepository,
	IUserRoleRepository userRoleRepository,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider
) : IUserRoleService
{
	public async Task<Result<Unit, AppException>> AssignAsync(
		Guid userId,
		Guid roleId,
		Guid assignedBy,
		CancellationToken ct = default)
	{
		RoleDto? role = await roleRepository.GetByIdAsync(roleId: roleId, ct: ct);
		if (role is null)
			return Result<Unit, AppException>.Failure(error: new NotFoundException(message: "Role not found.", id: roleId));

		UserRoleAggregate userRole = await LoadOrCreateAsync(userId: userId, ct: ct);

		Result<Unit, DomainException> assigned = userRole.Assign(
			occurredAt: dateProvider.UtcNow,
			roleId: roleId,
			assignedBy: assignedBy
		);
		if (assigned.IsFailure)
			return Result<Unit, AppException>.Failure(error: assigned.Error!);

		return await SaveAsync(userRole: userRole, ct: ct);
	}

	public async Task<Result<Unit, AppException>> RemoveAsync(
		Guid userId,
		Guid roleId,
		Guid removedBy,
		CancellationToken ct = default)
	{
		RoleDto? role = await roleRepository.GetByIdAsync(roleId: roleId, ct: ct);
		if (role is null)
			return Result<Unit, AppException>.Failure(error: new NotFoundException(message: "Role not found.", id: roleId));

		if (role.SystemKey == SystemRole.Root)
		{
			int rootHolders = await roleRepository.CountMembersWithSystemKeyAsync(
				systemKey: SystemRole.Root,
				ct: ct
			);

			if (rootHolders <= 1)
				return Result<Unit, AppException>.Failure(error: new LastRootRoleException());
		}

		UserRoleAggregate? userRole = await userRoleRepository.GetByUserIdAsync(userId: userId, ct: ct);

		if (userRole is null)
			return Result<Unit, AppException>.Success(value: Unit.Default);

		Result<Unit, DomainException> removed = userRole.Remove(
			occurredAt: dateProvider.UtcNow,
			roleId: roleId,
			removedBy: removedBy
		);
		if (removed.IsFailure)
			return Result<Unit, AppException>.Failure(error: removed.Error!);

		return await SaveAsync(userRole: userRole, ct: ct);
	}

	private async Task<UserRoleAggregate> LoadOrCreateAsync(Guid userId, CancellationToken ct)
	{
		UserRoleAggregate? existing = await userRoleRepository.GetByUserIdAsync(userId: userId, ct: ct);
		if (existing is not null)
			return existing;

		return UserRoleAggregate.Create(occurredAt: dateProvider.UtcNow, userId: userId).Value!;
	}

	private async Task<Result<Unit, AppException>> SaveAsync(UserRoleAggregate userRole, CancellationToken ct)
	{
		if (userRole.Events.Count == 0)
			return Result<Unit, AppException>.Success(value: Unit.Default);

		await unitOfWork.ExecuteInTransactionAsync(
			operation: async () => await userRoleRepository.SaveAsync(userRole: userRole, ct: ct),
			ct: ct
		);

		return Result<Unit, AppException>.Success(value: Unit.Default);
	}
}
