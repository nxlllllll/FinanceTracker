using FinanceTracker.Application.Services.Permissions;
using FinanceTracker.Application.UseCases.UserPermission.Commands.RevokePermission;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Permission;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.UserPermission;

public sealed class RevokePermissionHandlerTests
{
	private IUserPermissionService _userPermissionService = null!;
	private IRootAuthority _rootAuthority = null!;
	private RevokePermissionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userPermissionService = Substitute.For<IUserPermissionService>();
		_rootAuthority = Substitute.For<IRootAuthority>();

		_userPermissionService.RevokeAsync(
			targetUserId: Arg.Any<Guid>(),
			revokedBy: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Success(value: FinanceTracker.Core.Results.Unit.Default));

		_rootAuthority.IsRootAsync(userId: Arg.Any<Guid>()).Returns(returnThis: false);

		_handler = new RevokePermissionHandler(
			userPermissionService: _userPermissionService,
			rootAuthority: _rootAuthority
		);
	}

	[Test]
	public async Task Handle_ShouldPassTheCommandsPermissionToTheService()
	{
		Guid targetUserId = Guid.CreateVersion7();
		RevokePermissionCommand command = RevokePermissionCommandFactory.Create(targetUserId: targetUserId);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _userPermissionService.Received(requiredNumberOfCalls: 1).RevokeAsync(
			targetUserId: targetUserId,
			revokedBy: command.RevokedBy,
			permissions: Arg.Is<IReadOnlyCollection<Permission>>(predicate: p => p!.Count == 1 && p.Contains(command.Permission)),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenTargetEqualsRevokedBy_ShouldReturnFailureAndNotTouchTheService()
	{
		Guid userId = Guid.CreateVersion7();
		RevokePermissionCommand command = RevokePermissionCommandFactory.Create(targetUserId: userId, revokedBy: userId);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<SelfPermissionModificationException>();
		await _userPermissionService.DidNotReceive().RevokeAsync(
			targetUserId: Arg.Any<Guid>(),
			revokedBy: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenTargetEqualsRevokedByButUserIsRoot_ShouldDelegate()
	{
		Guid rootUserId = Guid.CreateVersion7();
		_rootAuthority.IsRootAsync(userId: rootUserId).Returns(returnThis: true);

		RevokePermissionCommand command = RevokePermissionCommandFactory.Create(targetUserId: rootUserId, revokedBy: rootUserId);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _userPermissionService.Received(requiredNumberOfCalls: 1).RevokeAsync(
			targetUserId: rootUserId,
			revokedBy: rootUserId,
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenTheServiceFails_ShouldReturnItsError()
	{
		_userPermissionService.RevokeAsync(
			targetUserId: Arg.Any<Guid>(),
			revokedBy: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Failure(error: new NotFoundException(message: "gone", id: Guid.Empty)));

		RevokePermissionCommand command = RevokePermissionCommandFactory.Create(targetUserId: Guid.CreateVersion7());

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}
}
