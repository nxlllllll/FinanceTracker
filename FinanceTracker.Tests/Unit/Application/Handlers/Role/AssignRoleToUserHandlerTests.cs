using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.Services.Permissions;
using FinanceTracker.Application.UseCases.Role.Commands.AssignRoleToUser;
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

public sealed class AssignRoleToUserHandlerTests
{
	private IRoleRepository _roleRepository = null!;
	private IUserPermissionService _userPermissionService = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private AssignRoleToUserHandler _handler = null!;

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

		_userPermissionService.GrantAsync(
			targetUserId: Arg.Any<Guid>(),
			grantedBy: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Success(value: FinanceTracker.Core.Results.Unit.Default));

		_handler = new AssignRoleToUserHandler(
			roleRepository: _roleRepository,
			userPermissionService: _userPermissionService,
			unitOfWork: _unitOfWork,
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default
		);
	}

	private static RoleDto BuildRole(Guid roleId, params Permission[] permissions) => new RoleDto(
		Id: roleId,
		SystemKey: null,
		DisplayName: Name.Create(value: "Test Role").Value!,
		Permissions: permissions.ToHashSet()
	);

	private void ReturnsRole(Guid roleId, RoleDto? role)
		=> _roleRepository.GetByIdAsync(roleId: roleId, ct: Arg.Any<CancellationToken>()).Returns(returnThis: role);

	[Test]
	public async Task Handle_WhenRoleNotFound_ShouldReturnFailure()
	{
		Guid roleId = Guid.CreateVersion7();
		ReturnsRole(roleId: roleId, role: null);

		AssignRoleToUserCommand command = new AssignRoleToUserCommand(
			UserId: Guid.CreateVersion7(),
			RoleId: roleId,
			AssignedBy: Guid.CreateVersion7()
		);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
	}

	[Test]
	public async Task Handle_WhenRoleExists_ShouldAssignRoleToUser()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();
		ReturnsRole(
			roleId: roleId,
			role: BuildRole(roleId: roleId, Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!)
		);

		AssignRoleToUserCommand command = new AssignRoleToUserCommand(
			UserId: userId,
			RoleId: roleId,
			AssignedBy: Guid.CreateVersion7()
		);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _roleRepository.Received(requiredNumberOfCalls: 1).AssignToUserAsync(
			userId: userId,
			roleId: roleId,
			assignedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_ShouldGrantTheRolesPermissionsInOneCall()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();
		Guid assignedBy = Guid.CreateVersion7();
		Permission accountRead = Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!;
		Permission budgetWrite = Permission.Create(resource: Resource.Budget, action: PermissionAction.Write).Value!;

		ReturnsRole(
			roleId: roleId,
			role: BuildRole(roleId: roleId, accountRead, budgetWrite)
		);

		AssignRoleToUserCommand command = new AssignRoleToUserCommand(
			UserId: userId,
			RoleId: roleId,
			AssignedBy: assignedBy
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _userPermissionService.Received(requiredNumberOfCalls: 1).GrantAsync(
			targetUserId: userId,
			grantedBy: assignedBy,
			permissions: Arg.Is<IReadOnlyCollection<Permission>>(predicate: p =>
				p!.Count == 2 && p.Contains(accountRead) && p.Contains(budgetWrite)
			),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_ShouldStageRoleAssignedNotification()
	{
		Guid roleId = Guid.CreateVersion7();
		ReturnsRole(roleId: roleId, role: BuildRole(roleId: roleId));

		AssignRoleToUserCommand command = new AssignRoleToUserCommand(
			UserId: Guid.CreateVersion7(),
			RoleId: roleId,
			AssignedBy: Guid.CreateVersion7()
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(
			notification: Arg.Any<RoleAssignedToUserNotification>()
		);
	}

	[Test]
	public async Task Handle_WhenGrantingFails_ShouldReturnFailureAndStageNothing()
	{
		Guid roleId = Guid.CreateVersion7();
		ReturnsRole(
			roleId: roleId,
			role: BuildRole(roleId: roleId, Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!)
		);

		_userPermissionService.GrantAsync(
			targetUserId: Arg.Any<Guid>(),
			grantedBy: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Failure(error: new SelfPermissionModificationException()));

		AssignRoleToUserCommand command = new AssignRoleToUserCommand(
			UserId: Guid.CreateVersion7(),
			RoleId: roleId,
			AssignedBy: Guid.CreateVersion7()
		);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<RoleAssignedToUserNotification>());
	}
}
