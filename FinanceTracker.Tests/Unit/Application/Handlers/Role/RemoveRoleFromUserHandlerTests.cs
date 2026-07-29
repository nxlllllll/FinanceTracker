using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.Services.Permissions;
using FinanceTracker.Application.UseCases.Role.Commands.RemoveRoleFromUser;
using FinanceTracker.Application.UseCases.Role.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Role;

public sealed class RemoveRoleFromUserHandlerTests
{
	private IRoleRepository _roleRepository = null!;
	private IUserPermissionService _userPermissionService = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private RemoveRoleFromUserHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_roleRepository = Substitute.For<IRoleRepository>();
		_userPermissionService = Substitute.For<IUserPermissionService>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task<Result<FinanceTracker.Core.Results.Unit, AppException>>>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task<Result<FinanceTracker.Core.Results.Unit, AppException>>>>()?.Invoke());

		_userPermissionService.RevokeAsync(
			targetUserId: Arg.Any<Guid>(),
			revokedBy: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Success(value: FinanceTracker.Core.Results.Unit.Default));

		_handler = new RemoveRoleFromUserHandler(
			roleRepository: _roleRepository,
			userPermissionService: _userPermissionService,
			unitOfWork: _unitOfWork,
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default
		);
	}

	private static RoleDto BuildRole(Guid roleId, SystemRole? systemKey, params Permission[] permissions) => new RoleDto(
		Id: roleId,
		SystemKey: systemKey,
		DisplayName: Name.Create(value: "Test Role").Value!,
		Permissions: permissions.ToHashSet()
	);

	private void ReturnsRole(Guid roleId, RoleDto? role)
	{
		_roleRepository.GetByIdAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: role);
	}

	private void ReturnsRootHolders(int count)
	{
		_roleRepository.CountMembersWithSystemKeyAsync(
			systemKey: SystemRole.Root,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: count);
	}

	private static RemoveRoleFromUserCommand Command(Guid roleId, Guid? userId = null)
	{
		return new RemoveRoleFromUserCommand(
			UserId: userId ?? Guid.CreateVersion7(),
			RoleId: roleId,
			RemovedBy: Guid.CreateVersion7()
		);
	}

	[Test]
	public async Task Handle_WhenRoleNotFound_ShouldReturnFailure()
	{
		Guid roleId = Guid.CreateVersion7();
		ReturnsRole(roleId: roleId, role: null);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: Command(roleId: roleId), ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
	}

	[Test]
	public async Task Handle_ForNonRootRole_ShouldRemoveAndRevokeItsPermissionsInOneCall()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();
		Permission budgetRead = Permission.Create(resource: Resource.Budget, action: PermissionAction.Read).Value!;
		ReturnsRole(
			roleId: roleId,
			role: BuildRole(roleId: roleId, systemKey: null, budgetRead)
		);

		RemoveRoleFromUserCommand command = Command(roleId: roleId, userId: userId);
		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _roleRepository.Received(requiredNumberOfCalls: 1).RemoveFromUserAsync(
			userId: userId,
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		);
		await _userPermissionService.Received(requiredNumberOfCalls: 1).RevokeAsync(
			targetUserId: userId,
			revokedBy: command.RemovedBy,
			permissions: Arg.Is<IReadOnlyCollection<Permission>>(predicate: p => p.Count == 1 && p.Contains(budgetRead)),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_ForRootRole_WhenOtherHoldersExist_ShouldSucceed()
	{
		Guid roleId = Guid.CreateVersion7();
		ReturnsRole(
			roleId: roleId,
			role: BuildRole(roleId: roleId, systemKey: SystemRole.Root)
		);
		ReturnsRootHolders(count: 2);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: Command(roleId: roleId), ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task Handle_ForRootRole_WhenOnlyOneHolderExists_ShouldFailWithLastRootRoleException()
	{
		Guid roleId = Guid.CreateVersion7();
		ReturnsRole(
			roleId: roleId,
			role: BuildRole(roleId: roleId, systemKey: SystemRole.Root)
		);
		ReturnsRootHolders(count: 1);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: Command(roleId: roleId), ct: CancellationToken.None);

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
		ReturnsRole(
			roleId: roleId,
			role: BuildRole(roleId: roleId, systemKey: null)
		);

		await _handler.Handle(command: Command(roleId: roleId), ct: CancellationToken.None);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(
			notification: Arg.Any<RoleRemovedFromUserNotification>()
		);
	}

	[Test]
	public async Task Handle_WhenRevokingFails_ShouldReturnFailureAndStageNothing()
	{
		Guid roleId = Guid.CreateVersion7();
		ReturnsRole(
			roleId: roleId,
			role: BuildRole(
				roleId: roleId,
				systemKey: null,
				Permission.Create(resource: Resource.Budget, action: PermissionAction.Read).Value!
			)
		);

		_userPermissionService.RevokeAsync(
			targetUserId: Arg.Any<Guid>(),
			revokedBy: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Failure(error: new NotFoundException(message: "gone", id: Guid.Empty)));

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: Command(roleId: roleId), ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<RoleRemovedFromUserNotification>());
	}
}
