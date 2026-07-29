using FinanceTracker.Application.Services.Permissions;
using FinanceTracker.Application.UseCases.UserPermission.Commands.GrantPermission;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.UserPermission;

public sealed class GrantPermissionHandlerTests
{
	private IUserPermissionService _userPermissionService = null!;
	private IRootAuthority _rootAuthority = null!;
	private GrantPermissionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userPermissionService = Substitute.For<IUserPermissionService>();
		_rootAuthority = Substitute.For<IRootAuthority>();

		_userPermissionService.GrantAsync(
			targetUserId: Arg.Any<Guid>(),
			grantedBy: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Success(value: FinanceTracker.Core.Results.Unit.Default));

		_rootAuthority.IsRootAsync(userId: Arg.Any<Guid>()).Returns(returnThis: false);

		_handler = new GrantPermissionHandler(
			userPermissionService: _userPermissionService,
			rootAuthority: _rootAuthority
		);
	}

	[Test]
	public async Task Handle_ShouldPassTheCommandsPermissionToTheService()
	{
		Guid targetUserId = Guid.CreateVersion7();
		GrantPermissionCommand command = GrantPermissionCommandFactory.Create(targetUserId: targetUserId);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _userPermissionService.Received(requiredNumberOfCalls: 1).GrantAsync(
			targetUserId: targetUserId,
			grantedBy: command.GrantedBy,
			permissions: Arg.Is<IReadOnlyCollection<Permission>>(predicate: p => p.Count == 1 && p.Contains(command.Permission)),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenTargetEqualsGrantedBy_ShouldReturnFailureAndNotTouchTheService()
	{
		Guid userId = Guid.CreateVersion7();
		GrantPermissionCommand command = GrantPermissionCommandFactory.Create(targetUserId: userId, grantedBy: userId);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<SelfPermissionModificationException>();
		await _userPermissionService.DidNotReceive().GrantAsync(
			targetUserId: Arg.Any<Guid>(),
			grantedBy: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenTargetEqualsGrantedByButUserIsRoot_ShouldDelegate()
	{
		Guid rootUserId = Guid.CreateVersion7();
		_rootAuthority.IsRootAsync(userId: rootUserId).Returns(returnThis: true);

		GrantPermissionCommand command = GrantPermissionCommandFactory.Create(targetUserId: rootUserId, grantedBy: rootUserId);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _userPermissionService.Received(requiredNumberOfCalls: 1).GrantAsync(
			targetUserId: rootUserId,
			grantedBy: rootUserId,
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenTheServiceFails_ShouldReturnItsError()
	{
		_userPermissionService.GrantAsync(
			targetUserId: Arg.Any<Guid>(),
			grantedBy: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Failure(error: new NotFoundException(message: "gone", id: Guid.Empty)));

		GrantPermissionCommand command = GrantPermissionCommandFactory.Create(targetUserId: Guid.CreateVersion7());

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}
}
