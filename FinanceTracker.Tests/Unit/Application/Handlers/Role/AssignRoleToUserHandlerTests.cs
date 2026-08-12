using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.Services.Roles;
using FinanceTracker.Application.UseCases.Role.Commands.AssignRoleToUser;
using FinanceTracker.Application.UseCases.Role.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Role;

public sealed class AssignRoleToUserHandlerTests
{
	private IUserRoleService _userRoleService = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private AssignRoleToUserHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userRoleService = Substitute.For<IUserRoleService>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();

		_userRoleService.AssignAsync(
			userId: Arg.Any<Guid>(),
			roleId: Arg.Any<Guid>(),
			assignedBy: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Success(value: FinanceTracker.Core.Results.Unit.Default));

		_handler = new AssignRoleToUserHandler(
			userRoleService: _userRoleService,
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default
		);
	}

	private static AssignRoleToUserCommand Command() => new AssignRoleToUserCommand(
		UserId: Guid.CreateVersion7(),
		RoleId: Guid.CreateVersion7(),
		AssignedBy: Guid.CreateVersion7()
	);

	[Test]
	public async Task Handle_ShouldPassTheCommandToTheService()
	{
		AssignRoleToUserCommand command = Command();

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _userRoleService.Received(requiredNumberOfCalls: 1).AssignAsync(
			userId: command.UserId,
			roleId: command.RoleId,
			assignedBy: command.AssignedBy,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_OnSuccess_ShouldStageRoleAssignedNotification()
	{
		await _handler.Handle(command: Command(), ct: CancellationToken.None);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(
			notification: Arg.Any<RoleAssignedToUserNotification>()
		);
	}

	[Test]
	public async Task Handle_WhenTheServiceFails_ShouldReturnItsErrorAndStageNothing()
	{
		_userRoleService.AssignAsync(
			userId: Arg.Any<Guid>(),
			roleId: Arg.Any<Guid>(),
			assignedBy: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Failure(error: new NotFoundException(message: "Role not found.", id: Guid.Empty)));

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: Command(), ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<RoleAssignedToUserNotification>());
	}
}
