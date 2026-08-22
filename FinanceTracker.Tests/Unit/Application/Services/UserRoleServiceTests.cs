using FinanceTracker.Application.Services.Roles;
using FinanceTracker.Core.Domains.UserRole.Events;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Role;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Repositories.UserRole;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;
using UserRoleAggregate = FinanceTracker.Core.Domains.UserRole.UserRole;

namespace FinanceTracker.Tests.Unit.Application.Services;

public sealed class UserRoleServiceTests
{
	private IRoleRepository _roleRepository = null!;
	private IUserRoleRepository _userRoleRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private UserRoleService _service = null!;

	private static readonly Permission AccountRead = Permission.Create(
		resource: Resource.Account,
		action: PermissionAction.Read
	).Value!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_roleRepository = Substitute.For<IRoleRepository>();
		_userRoleRepository = Substitute.For<IUserRoleRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()?.Invoke());

		_service = new UserRoleService(
			roleRepository: _roleRepository,
			userRoleRepository: _userRoleRepository,
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

	private void ReturnsMembership(
		Guid userId,
		UserRoleAggregate? aggregate
	) => _userRoleRepository.GetByUserIdAsync(
		userId: userId,
		ct: Arg.Any<CancellationToken>()
	).Returns(returnThis: aggregate);

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
	public async Task AssignAsync_ForAUserWithNoMembershipYet_ShouldCreateTheAggregateAndRecordTheRole()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();
		Guid assignedBy = Guid.CreateVersion7();
		ReturnsRole(roleId: roleId, role: BuildRole(roleId: roleId, systemKey: null, AccountRead));
		ReturnsMembership(userId: userId, aggregate: null);

		UserRoleAggregate? saved = null;
		await _userRoleRepository.SaveAsync(
			userRole: Arg.Do<UserRoleAggregate>(useArgument: a => saved = a),
			ct: Arg.Any<CancellationToken>()
		);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _service.AssignAsync(userId: userId, roleId: roleId, assignedBy: assignedBy);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: saved).IsNotNull();
		await Assert.That(value: saved!.RoleIds).Contains(expected: roleId);
		await Assert.That(value: saved.Events.OfType<RoleAssigned>().Single().AssignedBy).IsEqualTo(expected: assignedBy);
	}

	[Test]
	public async Task AssignAsync_ShouldRaiseOnlyMembershipEvents()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();
		ReturnsRole(roleId: roleId, role: BuildRole(roleId: roleId, systemKey: null, AccountRead));
		ReturnsMembership(userId: userId, aggregate: null);

		UserRoleAggregate? saved = null;
		await _userRoleRepository.SaveAsync(
			userRole: Arg.Do<UserRoleAggregate>(useArgument: a => saved = a),
			ct: Arg.Any<CancellationToken>()
		);

		await _service.AssignAsync(userId: userId, roleId: roleId, assignedBy: Guid.CreateVersion7());

		await Assert.That(value: saved!.Events.Count).IsEqualTo(expected: 2).Because(message: """
			Creation and the assignment itself, and nothing else. What the role grants must not be copied
			onto the user — the moment it is, the two sources become indistinguishable again and removing
			a role starts taking away access it never provided.
		""");
	}

	[Test]
	public async Task AssignAsync_ForARoleTheUserAlreadyHolds_ShouldSucceedWithoutSaving()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();
		ReturnsRole(roleId: roleId, role: BuildRole(roleId: roleId, systemKey: null, AccountRead));
		ReturnsMembership(userId: userId, aggregate: UserRoleFactory.CreateWithRole(userId: userId, roleId: roleId));

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _service.AssignAsync(
			userId: userId,
			roleId: roleId,
			assignedBy: Guid.CreateVersion7()
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _userRoleRepository.DidNotReceive().SaveAsync(
			userRole: Arg.Any<UserRoleAggregate>(),
			ct: Arg.Any<CancellationToken>()
		);
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

		await _userRoleRepository.DidNotReceive().SaveAsync(
			userRole: Arg.Any<UserRoleAggregate>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task RemoveAsync_ForRootWhenOtherHoldersExist_ShouldSucceed()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();
		ReturnsRole(roleId: roleId, role: BuildRole(roleId: roleId, systemKey: SystemRole.Root));
		ReturnsRootHolders(count: 2);
		ReturnsMembership(userId: userId, aggregate: UserRoleFactory.CreateWithRole(userId: userId, roleId: roleId));

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _service.RemoveAsync(
			userId: userId,
			roleId: roleId,
			removedBy: Guid.CreateVersion7()
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task RemoveAsync_ShouldRecordTheRemovalWithItsAuthor()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();
		Guid removedBy = Guid.CreateVersion7();
		ReturnsRole(roleId: roleId, role: BuildRole(roleId: roleId, systemKey: null, AccountRead));
		ReturnsMembership(userId: userId, aggregate: UserRoleFactory.CreateWithRole(userId: userId, roleId: roleId));

		UserRoleAggregate? saved = null;
		await _userRoleRepository.SaveAsync(
			userRole: Arg.Do<UserRoleAggregate>(useArgument: a => saved = a),
			ct: Arg.Any<CancellationToken>()
		);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _service.RemoveAsync(userId: userId, roleId: roleId, removedBy: removedBy);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: saved!.RoleIds).IsEmpty();
		await Assert.That(value: saved.Events.OfType<RoleRemoved>().Single().RemovedBy).IsEqualTo(expected: removedBy);
	}

	[Test]
	public async Task RemoveAsync_ForAUserWithNoMembershipAtAll_ShouldSucceedWithoutSaving()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();
		ReturnsRole(roleId: roleId, role: BuildRole(roleId: roleId, systemKey: null, AccountRead));
		ReturnsMembership(userId: userId, aggregate: null);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _service.RemoveAsync(
			userId: userId,
			roleId: roleId,
			removedBy: Guid.CreateVersion7()
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _userRoleRepository.DidNotReceive().SaveAsync(
			userRole: Arg.Any<UserRoleAggregate>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
