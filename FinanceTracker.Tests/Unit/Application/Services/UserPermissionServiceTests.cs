using FinanceTracker.Application.Services.Permissions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;
using UserPermissionAggregate = FinanceTracker.Core.Domains.UserPermission.UserPermission;

namespace FinanceTracker.Tests.Unit.Application.Services;

public sealed class UserPermissionServiceTests
{
	private static Permission AccountWrite => Permission.Create(
		resource: Resource.Account,
		action: PermissionAction.Write
	).Value!;
	private static Permission BalanceRead => Permission.Create(
		resource: Resource.Balance,
		action: PermissionAction.Read
	).Value!;

	private IUserPermissionRepository _userPermissionRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private UserPermissionService _service = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userPermissionRepository = Substitute.For<IUserPermissionRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()?.Invoke());

		_service = new UserPermissionService(
			userPermissionRepository: _userPermissionRepository,
			unitOfWork: _unitOfWork,
			dateProvider: FakeDateProvider.Default
		);
	}

	private void ReturnsAggregate(Guid userId, UserPermissionAggregate? aggregate)
	{
		_userPermissionRepository.GetByUserIdAsync(
			userId: userId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: aggregate);
	}

	[Test]
	public async Task GrantAsync_WithNoExistingAggregate_ShouldCreateAndSave()
	{
		Guid userId = Guid.CreateVersion7();
		ReturnsAggregate(userId: userId, aggregate: null);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _service.GrantAsync(
			targetUserId: userId,
			grantedBy: Guid.CreateVersion7(),
			permissions: [AccountWrite]
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _userPermissionRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			userPermission: Arg.Is<UserPermissionAggregate>(predicate: up => up!.UserId == userId && up.Permissions.Contains("account:write")),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GrantAsync_WithSeveralPermissions_ShouldSaveOnce()
	{
		Guid userId = Guid.CreateVersion7();
		UserPermissionAggregate existing = UserPermissionFactory.Create(userId: userId);
		existing.ClearEvents();
		ReturnsAggregate(userId: userId, aggregate: existing);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _service.GrantAsync(
			targetUserId: userId,
			grantedBy: Guid.CreateVersion7(),
			permissions: [AccountWrite, BalanceRead]
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: existing.Permissions).Contains(expected: "account:write");
		await Assert.That(value: existing.Permissions).Contains(expected: "balance:read");

		await _userPermissionRepository.Received(requiredNumberOfCalls: 1).GetByUserIdAsync(
			userId: userId,
			ct: Arg.Any<CancellationToken>()
		);
		await _userPermissionRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			userPermission: Arg.Any<UserPermissionAggregate>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GrantAsync_WhenEveryPermissionIsAlreadyHeld_ShouldNotSave()
	{
		Guid userId = Guid.CreateVersion7();
		UserPermissionAggregate existing = UserPermissionFactory.CreateWithGrant(
			userId: userId,
			resource: Resource.Account,
			action: PermissionAction.Write
		);
		existing.ClearEvents();
		ReturnsAggregate(userId: userId, aggregate: existing);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _service.GrantAsync(
			targetUserId: userId,
			grantedBy: Guid.CreateVersion7(),
			permissions: [AccountWrite]
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: existing.Events).IsEmpty();

		await _userPermissionRepository.DidNotReceive().SaveAsync(
			userPermission: Arg.Any<UserPermissionAggregate>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GrantAsync_WithAnEmptyBatch_ShouldNotEvenLoadTheAggregate()
	{
		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _service.GrantAsync(
			targetUserId: Guid.CreateVersion7(),
			grantedBy: Guid.CreateVersion7(),
			permissions: []
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _userPermissionRepository.DidNotReceive().GetByUserIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task RevokeAsync_ShouldRemoveEveryPermissionInTheBatchAndSaveOnce()
	{
		Guid userId = Guid.CreateVersion7();
		UserPermissionAggregate existing = UserPermissionFactory.CreateWithGrant(
			userId: userId,
			resource: Resource.Account,
			action: PermissionAction.Write
		);
		existing.ClearEvents();
		ReturnsAggregate(userId: userId, aggregate: existing);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _service.RevokeAsync(
			targetUserId: userId,
			revokedBy: Guid.CreateVersion7(),
			permissions: [AccountWrite, BalanceRead]
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: existing.Permissions).DoesNotContain(expected: "account:write");
		await _userPermissionRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			userPermission: Arg.Any<UserPermissionAggregate>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task RevokeAsync_WithNoExistingAggregate_ShouldSucceedWithoutSaving()
	{
		Guid userId = Guid.CreateVersion7();
		ReturnsAggregate(userId: userId, aggregate: null);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _service.RevokeAsync(
			targetUserId: userId,
			revokedBy: Guid.CreateVersion7(),
			permissions: [AccountWrite]
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _userPermissionRepository.DidNotReceive().SaveAsync(
			userPermission: Arg.Any<UserPermissionAggregate>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task RevokeAsync_WhenNothingIsHeld_ShouldNotSave()
	{
		Guid userId = Guid.CreateVersion7();
		UserPermissionAggregate existing = UserPermissionFactory.Create(userId: userId);
		existing.ClearEvents();
		ReturnsAggregate(userId: userId, aggregate: existing);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _service.RevokeAsync(
			targetUserId: userId,
			revokedBy: Guid.CreateVersion7(),
			permissions: [AccountWrite]
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: existing.Events).IsEmpty();
		await _userPermissionRepository.DidNotReceive().SaveAsync(
			userPermission: Arg.Any<UserPermissionAggregate>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
