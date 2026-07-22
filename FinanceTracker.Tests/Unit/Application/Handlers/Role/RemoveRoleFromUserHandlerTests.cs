using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Role.Commands.RemoveRoleFromUser;
using FinanceTracker.Application.UseCases.UserPermission.Commands.RevokePermission;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Role;

public sealed class RemoveRoleFromUserHandlerTests
{
	private IRoleRepository _roleRepository = null!;
	private ISender _sender = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private RemoveRoleFromUserHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_roleRepository = Substitute.For<IRoleRepository>();
		_sender = Substitute.For<ISender>();
		_sender.Send(
			request: Arg.Any<RevokePermissionCommand>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Success(value: FinanceTracker.Core.Results.Unit.Default));
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();

		_handler = new RemoveRoleFromUserHandler(
			roleRepository: _roleRepository,
			sender: _sender,
			dateProvider: FakeDateProvider.Default,
			postCommitNotifications: _postCommitNotifications
		);
	}

	private static RoleDto BuildRole(Guid roleId, SystemRole? systemKey, params Permission[] permissions) => new RoleDto(
		Id: roleId,
		SystemKey: systemKey,
		DisplayName: Name.Create(value: "Test Role").Value!,
		Permissions: permissions.ToHashSet()
	);

	[Test]
	public async Task Handle_WhenRoleNotFound_ShouldReturnFailure()
	{
		Guid roleId = Guid.CreateVersion7();
		_roleRepository.GetByIdAsync(roleId: roleId, ct: Arg.Any<CancellationToken>()).Returns(returnThis: (RoleDto?)null);

		RemoveRoleFromUserCommand command = new RemoveRoleFromUserCommand(
			UserId: Guid.CreateVersion7(),
			RoleId: roleId,
			RemovedBy: Guid.CreateVersion7()
		);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
	}

	[Test]
	public async Task Handle_ForNonRootRole_ShouldRemoveAndRevokeAllPermissions()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();
		Permission budgetRead = Permission.Create(resource: Resource.Budget, action: PermissionAction.Read).Value!;
		_roleRepository.GetByIdAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: BuildRole(roleId: roleId, systemKey: null, budgetRead));

		RemoveRoleFromUserCommand command = new RemoveRoleFromUserCommand(
			UserId: userId,
			RoleId: roleId,
			RemovedBy: Guid.CreateVersion7()
		);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _roleRepository.Received(requiredNumberOfCalls: 1).RemoveFromUserAsync(
			userId: userId,
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		);
		await _sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<RevokePermissionCommand>(predicate: c => c!.TargetUserId == userId && c.Permission == budgetRead),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenTargetEqualsRevokedBy_ShouldReturnFailureAndNotSave()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();
		_roleRepository.GetByIdAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: BuildRole(roleId: roleId, systemKey: SystemRole.Root));
		_roleRepository.CountMembersWithSystemKeyAsync(
			systemKey: SystemRole.Root,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 2);

		RemoveRoleFromUserCommand command = new RemoveRoleFromUserCommand(
			UserId: userId,
			RoleId: roleId,
			RemovedBy: Guid.CreateVersion7()
		);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task Handle_ForRootRole_WhenOnlyOneHolderExists_ShouldFailWithLastRootRoleException()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();
		_roleRepository.GetByIdAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: BuildRole(roleId: roleId, systemKey: SystemRole.Root));
		_roleRepository.CountMembersWithSystemKeyAsync(
			systemKey: SystemRole.Root,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 1);

		RemoveRoleFromUserCommand command = new RemoveRoleFromUserCommand(
			UserId: userId,
			RoleId: roleId,
			RemovedBy: Guid.CreateVersion7()
		);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<LastRootRoleException>();
		await _roleRepository.DidNotReceive().RemoveFromUserAsync(
			userId: Arg.Any<Guid>(),
			roleId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_OnSuccess_ShouldStageRoleRemovedNotification()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();
		_roleRepository.GetByIdAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: BuildRole(roleId: roleId, systemKey: null));

		RemoveRoleFromUserCommand command = new RemoveRoleFromUserCommand(
			UserId: userId,
			RoleId: roleId,
			RemovedBy: Guid.CreateVersion7()
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(
			notification: Arg.Any<FinanceTracker.Application.UseCases.Role.Notifications.RoleRemovedFromUserNotification>()
		);
	}
}
