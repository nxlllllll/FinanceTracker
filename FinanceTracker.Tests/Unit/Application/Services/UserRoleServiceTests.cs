using FinanceTracker.Application.Services.Permissions;
using FinanceTracker.Application.Services.Roles;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Services;

public sealed class UserRoleServiceTests
{
	private IRoleRepository _roleRepository = null!;
	private IUserPermissionService _userPermissionService = null!;
	private IUnitOfWork _unitOfWork = null!;
	private UserRoleService _service = null!;

	private static readonly Permission AccountRead = Permission.Create(
		resource: Resource.Account,
		action: PermissionAction.Read
	).Value!;
	private static readonly Permission BudgetWrite = Permission.Create(
		resource: Resource.Budget,
		action: PermissionAction.Write
	).Value!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_roleRepository = Substitute.For<IRoleRepository>();
		_userPermissionService = Substitute.For<IUserPermissionService>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task<Result<FinanceTracker.Core.Results.Unit, AppException>>>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task<Result<FinanceTracker.Core.Results.Unit, AppException>>>>()?.Invoke());

		_userPermissionService.GrantAsync(
			targetUserId: Arg.Any<Guid>(),
			grantedBy: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Success(value: FinanceTracker.Core.Results.Unit.Default));

		_userPermissionService.RevokeAsync(
			targetUserId: Arg.Any<Guid>(),
			revokedBy: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Success(value: FinanceTracker.Core.Results.Unit.Default));

		_service = new UserRoleService(
			roleRepository: _roleRepository,
			userPermissionService: _userPermissionService,
			unitOfWork: _unitOfWork,
			dateProvider: FakeDateProvider.Default
		);
	}

	private static RoleDto BuildRole(
		Guid roleId,
		SystemRole? systemKey,
		params Permission[] permissions
	) => new RoleDto(
		Id: roleId,
		SystemKey: systemKey,
		DisplayName: Name.Create(value: "Test Role").Value!,
		Permissions: permissions.ToHashSet()
	);

	private void ReturnsRole(
		Guid roleId,
		RoleDto? role
	) => _roleRepository.GetByIdAsync(
		roleId: roleId,
		ct: Arg.Any<CancellationToken>()
	).Returns(returnThis: role);

	private void ReturnsRootHolders(
		int count
	) => _roleRepository.CountMembersWithSystemKeyAsync(
		systemKey: SystemRole.Root,
		ct: Arg.Any<CancellationToken>()
	).Returns(returnThis: count);

	[Test]
	public async Task AssignAsync_WhenRoleNotFound_ShouldReturnFailure()
	{
		Guid roleId = Guid.CreateVersion7();
		ReturnsRole(roleId: roleId, role: null);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _service.AssignAsync(
			userId: Guid.CreateVersion7(),
			roleId: roleId,
			assignedBy: Guid.CreateVersion7()
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task AssignAsync_ShouldAddTheMembershipAndGrantItsPermissionsInOneCall()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();
		Guid assignedBy = Guid.CreateVersion7();
		ReturnsRole(
			roleId: roleId,
			role: BuildRole(roleId: roleId, systemKey: null, AccountRead, BudgetWrite)
		);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _service.AssignAsync(userId: userId, roleId: roleId, assignedBy: assignedBy);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _roleRepository.Received(requiredNumberOfCalls: 1).AssignToUserAsync(
			userId: userId,
			roleId: roleId,
			assignedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);

		await _userPermissionService.Received(requiredNumberOfCalls: 1).GrantAsync(
			targetUserId: userId,
			grantedBy: assignedBy,
			permissions: Arg.Is<IReadOnlyCollection<Permission>>(predicate: p =>
				p!.Count == 2 && p.Contains(AccountRead) && p.Contains(BudgetWrite)
			),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task AssignAsync_WhenGrantingFails_ShouldReturnItsError()
	{
		Guid roleId = Guid.CreateVersion7();
		ReturnsRole(
			roleId: roleId,
			role: BuildRole(roleId: roleId, systemKey: null, AccountRead)
		);

		_userPermissionService.GrantAsync(
			targetUserId: Arg.Any<Guid>(),
			grantedBy: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Failure(error: new SelfPermissionModificationException()));

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _service.AssignAsync(
			userId: Guid.CreateVersion7(),
			roleId: roleId,
			assignedBy: Guid.CreateVersion7()
		);

		await Assert.That(value: result.IsFailure).IsTrue();
	}

	[Test]
	public async Task RemoveAsync_WhenRoleNotFound_ShouldReturnFailure()
	{
		Guid roleId = Guid.CreateVersion7();
		ReturnsRole(roleId: roleId, role: null);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _service.RemoveAsync(
			userId: Guid.CreateVersion7(),
			roleId: roleId,
			removedBy: Guid.CreateVersion7()
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task RemoveAsync_ForTheLastRootHolder_ShouldRefuseAndRemoveNothing()
	{
		Guid roleId = Guid.CreateVersion7();
		ReturnsRole(roleId: roleId, role: BuildRole(roleId: roleId, systemKey: SystemRole.Root));
		ReturnsRootHolders(count: 1);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _service.RemoveAsync(
			userId: Guid.CreateVersion7(),
			roleId: roleId,
			removedBy: Guid.CreateVersion7()
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<LastRootRoleException>();

		await _roleRepository.DidNotReceive().RemoveFromUserAsync(
			userId: Arg.Any<Guid>(),
			roleId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task RemoveAsync_ForRootWhenOtherHoldersExist_ShouldSucceed()
	{
		Guid roleId = Guid.CreateVersion7();
		ReturnsRole(roleId: roleId, role: BuildRole(roleId: roleId, systemKey: SystemRole.Root));
		ReturnsRootHolders(count: 2);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _service.RemoveAsync(
			userId: Guid.CreateVersion7(),
			roleId: roleId,
			removedBy: Guid.CreateVersion7()
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task RemoveAsync_ShouldDropTheMembershipAndRevokeItsPermissionsInOneCall()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();
		Guid removedBy = Guid.CreateVersion7();
		ReturnsRole(
			roleId: roleId,
			role: BuildRole(roleId: roleId, systemKey: null, AccountRead)
		);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _service.RemoveAsync(userId: userId, roleId: roleId, removedBy: removedBy);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _roleRepository.Received(requiredNumberOfCalls: 1).RemoveFromUserAsync(
			userId: userId,
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		);
		await _userPermissionService.Received(requiredNumberOfCalls: 1).RevokeAsync(
			targetUserId: userId,
			revokedBy: removedBy,
			permissions: Arg.Is<IReadOnlyCollection<Permission>>(predicate: p => p!.Count == 1 && p.Contains(AccountRead)),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
