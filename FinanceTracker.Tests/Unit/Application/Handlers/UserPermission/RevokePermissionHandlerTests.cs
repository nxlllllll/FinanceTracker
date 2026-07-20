using FinanceTracker.Application.UseCases.UserPermission.Commands.RevokePermission;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.UserPermission;

public sealed class RevokePermissionHandlerTests
{
	private IUserPermissionRepository _userPermissionRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private RevokePermissionHandler _handler = null!;
	private IRootAuthority _rootAuthority = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userPermissionRepository = Substitute.For<IUserPermissionRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_rootAuthority = Substitute.For<IRootAuthority>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()?.Invoke());

		_rootAuthority.IsRoot(userId: Arg.Any<Guid>()).Returns(returnThis: false);

		_handler = new RevokePermissionHandler(
			userPermissionRepository: _userPermissionRepository,
			unitOfWork: _unitOfWork,
			rootAuthority: _rootAuthority,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task Handle_WithNoExistingAggregate_ShouldBeNoOpSuccess()
	{
		Guid targetUserId = Guid.CreateVersion7();
		_userPermissionRepository.GetByUserIdAsync(
			userId: targetUserId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (FinanceTracker.Core.Domains.UserPermission.UserPermission?)null);

		RevokePermissionCommand command = RevokePermissionCommandFactory.Create(targetUserId: targetUserId);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _userPermissionRepository.DidNotReceive().SaveAsync(
			userPermission: Arg.Any<FinanceTracker.Core.Domains.UserPermission.UserPermission>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WithHeldPermission_ShouldRevokeAndSave()
	{
		Guid targetUserId = Guid.CreateVersion7();
		FinanceTracker.Core.Domains.UserPermission.UserPermission existing = UserPermissionFactory.CreateWithGrant(
			userId: targetUserId,
			resource: Resource.Transaction,
			action: PermissionAction.Delete
		);
		_userPermissionRepository.GetByUserIdAsync(
			userId: targetUserId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: existing);

		RevokePermissionCommand command = RevokePermissionCommandFactory.Create(
			targetUserId: targetUserId,
			resource: Resource.Transaction,
			action: PermissionAction.Delete
		);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: existing.Permissions).DoesNotContain(expected: "transaction:delete");
		await _userPermissionRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			userPermission: Arg.Any<FinanceTracker.Core.Domains.UserPermission.UserPermission>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenTargetEqualsRevokedBy_ShouldReturnFailureAndNotSave()
	{
		Guid userId = Guid.CreateVersion7();
		RevokePermissionCommand command = RevokePermissionCommandFactory.Create(targetUserId: userId, revokedBy: userId);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<SelfPermissionModificationException>();
		await _userPermissionRepository.DidNotReceive().SaveAsync(
			userPermission: Arg.Any<FinanceTracker.Core.Domains.UserPermission.UserPermission>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenTargetEqualsGrantedByButUserIsRoot_ShouldSucceed()
	{
		Guid rootUserId = Guid.CreateVersion7();
		IRootAuthority rootAuthority = Substitute.For<IRootAuthority>();
		rootAuthority.IsRoot(userId: rootUserId).Returns(returnThis: true);

		RevokePermissionHandler handler = new RevokePermissionHandler(
			userPermissionRepository: _userPermissionRepository,
			unitOfWork: _unitOfWork,
			dateProvider: FakeDateProvider.Default,
			rootAuthority: rootAuthority
		);

		RevokePermissionCommand command = RevokePermissionCommandFactory.Create(targetUserId: rootUserId, revokedBy: rootUserId);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
	}
}
