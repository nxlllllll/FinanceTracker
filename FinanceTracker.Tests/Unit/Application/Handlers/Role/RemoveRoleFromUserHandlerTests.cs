using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.Services.Roles;
using FinanceTracker.Application.UseCases.Role.Commands.RemoveRoleFromUser;
using FinanceTracker.Application.UseCases.Role.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Role;

public sealed class RemoveRoleFromUserHandlerTests
{
	private IUserRoleService _userRoleService = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private RemoveRoleFromUserHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userRoleService = Substitute.For<IUserRoleService>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();

		_userRoleService.RemoveAsync(
			userId: Arg.Any<Guid>(),
			roleId: Arg.Any<Guid>(),
			removedBy: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Success(value: FinanceTracker.Core.Results.Unit.Default));

		_handler = new RemoveRoleFromUserHandler(
			userRoleService: _userRoleService,
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default
		);
	}

	private static RemoveRoleFromUserCommand Command() => new RemoveRoleFromUserCommand(
		UserId: Guid.CreateVersion7(),
		RoleId: Guid.CreateVersion7(),
		RemovedBy: Guid.CreateVersion7()
	);

	[Test]
	public async Task Handle_ShouldPassTheCommandToTheService()
	{
		RemoveRoleFromUserCommand command = Command();

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _userRoleService.Received(requiredNumberOfCalls: 1).RemoveAsync(
			userId: command.UserId,
			roleId: command.RoleId,
			removedBy: command.RemovedBy,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_OnSuccess_ShouldStageRoleRemovedNotification()
	{
		await _handler.Handle(command: Command(), ct: CancellationToken.None);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(
			notification: Arg.Any<RoleRemovedFromUserNotification>()
		);
	}

	[Test]
	public async Task Handle_WhenTheServiceRefuses_ShouldReturnItsErrorAndStageNothing()
	{
		_userRoleService.RemoveAsync(
			userId: Arg.Any<Guid>(),
			roleId: Arg.Any<Guid>(),
			removedBy: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Failure(error: new LastRootRoleException()));

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: Command(), ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<LastRootRoleException>();
		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<RoleRemovedFromUserNotification>());
	}
}
