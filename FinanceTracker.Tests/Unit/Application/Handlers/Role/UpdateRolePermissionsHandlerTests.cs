using FinanceTracker.Application.Services.Permissions;
using FinanceTracker.Application.UseCases.Role.Commands.UpdateRolePermissions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Role;

public sealed class UpdateRolePermissionsHandlerTests
{
	private static readonly Permission AccountRead = Permission.Create(
		resource: Resource.Account,
		action: PermissionAction.Read
	).Value!;
	private static readonly Permission BudgetWrite = Permission.Create(
		resource: Resource.Budget,
		action: PermissionAction.Write
	).Value!;

	private IRoleRepository _roleRepository = null!;
	private IUserPermissionService _userPermissionService = null!;
	private IUnitOfWork _unitOfWork = null!;
	private UpdateRolePermissionsHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_roleRepository = Substitute.For<IRoleRepository>();
		_userPermissionService = Substitute.For<IUserPermissionService>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

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

		_userPermissionService.RevokeAsync(
			targetUserId: Arg.Any<Guid>(),
			revokedBy: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Success(value: FinanceTracker.Core.Results.Unit.Default));

		_handler = new UpdateRolePermissionsHandler(
			roleRepository: _roleRepository,
			userPermissionService: _userPermissionService,
			unitOfWork: _unitOfWork
		);
	}

	private static RoleDto BuildRole(Guid roleId, params Permission[] permissions) => new RoleDto(
		Id: roleId,
		SystemKey: null,
		DisplayName: Name.Create(value: "Test Role").Value!,
		Permissions: permissions.ToHashSet()
	);

	private void Arrange(Guid roleId, RoleDto? role, params Guid[] memberUserIds)
	{
		_roleRepository.GetByIdAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: role);
		_roleRepository.GetMemberUserIdsAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: memberUserIds.ToList());
	}

	[Test]
	public async Task Handle_WhenRoleNotFound_ShouldReturnFailure()
	{
		Guid roleId = Guid.CreateVersion7();
		Arrange(roleId: roleId, role: null);

		UpdateRolePermissionsCommand command = new UpdateRolePermissionsCommand(
			RoleId: roleId,
			NewPermissions: new HashSet<Permission>(),
			UpdatedBy: Guid.CreateVersion7()
		);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
	}

	[Test]
	public async Task Handle_ShouldGrantOnlyNewlyAddedPermissionsToExistingMembers()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid memberUserId = Guid.CreateVersion7();
		Arrange(
			roleId: roleId,
			role: BuildRole(roleId: roleId, AccountRead),
			memberUserIds: memberUserId
		);

		UpdateRolePermissionsCommand command = new UpdateRolePermissionsCommand(
			RoleId: roleId,
			NewPermissions: new HashSet<Permission> { AccountRead, BudgetWrite },
			UpdatedBy: Guid.CreateVersion7()
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _userPermissionService.Received(requiredNumberOfCalls: 1).GrantAsync(
			targetUserId: memberUserId,
			grantedBy: command.UpdatedBy,
			permissions: Arg.Is<IReadOnlyCollection<Permission>>(predicate: p => p!.Count == 1 && p.Contains(BudgetWrite)),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_ShouldRevokeOnlyRemovedPermissionsFromExistingMembers()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid memberUserId = Guid.CreateVersion7();
		Arrange(
			roleId: roleId,
			role: BuildRole(roleId: roleId, AccountRead, BudgetWrite),
			memberUserIds: memberUserId
		);

		UpdateRolePermissionsCommand command = new UpdateRolePermissionsCommand(
			RoleId: roleId,
			NewPermissions: new HashSet<Permission> { AccountRead },
			UpdatedBy: Guid.CreateVersion7()
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _userPermissionService.Received(requiredNumberOfCalls: 1).RevokeAsync(
			targetUserId: memberUserId,
			revokedBy: command.UpdatedBy,
			permissions: Arg.Is<IReadOnlyCollection<Permission>>(predicate: p => p!.Count == 1 && p.Contains(BudgetWrite)),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WithSeveralMembers_ShouldCallTheServiceTwicePerMember()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid firstMember = Guid.CreateVersion7();
		Guid secondMember = Guid.CreateVersion7();
		Arrange(
			roleId: roleId,
			role: BuildRole(roleId: roleId, AccountRead),
			memberUserIds: [firstMember, secondMember]
		);

		UpdateRolePermissionsCommand command = new UpdateRolePermissionsCommand(
			RoleId: roleId,
			NewPermissions: new HashSet<Permission> { BudgetWrite },
			UpdatedBy: Guid.CreateVersion7()
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _userPermissionService.Received(requiredNumberOfCalls: 2).GrantAsync(
			targetUserId: Arg.Any<Guid>(),
			grantedBy: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _userPermissionService.Received(requiredNumberOfCalls: 2).RevokeAsync(
			targetUserId: Arg.Any<Guid>(),
			revokedBy: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlyCollection<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_ShouldReplacePermissionsInRepository()
	{
		Guid roleId = Guid.CreateVersion7();
		Arrange(roleId: roleId, role: BuildRole(roleId: roleId));

		IReadOnlySet<Permission> newPermissions = new HashSet<Permission>
		{
			Permission.Create(resource: Resource.Category, action: PermissionAction.Delete).Value!
		};
		UpdateRolePermissionsCommand command = new UpdateRolePermissionsCommand(
			RoleId: roleId,
			NewPermissions: newPermissions,
			UpdatedBy: Guid.CreateVersion7()
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _roleRepository.Received(requiredNumberOfCalls: 1).ReplacePermissionsAsync(
			roleId: roleId,
			permissions: newPermissions,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenNothingChanges_ShouldNotTouchTheRepositoryOrMembers()
	{
		Guid roleId = Guid.CreateVersion7();
		Arrange(
			roleId: roleId,
			role: BuildRole(roleId: roleId, AccountRead),
			memberUserIds: Guid.CreateVersion7()
		);

		UpdateRolePermissionsCommand command = new UpdateRolePermissionsCommand(
			RoleId: roleId,
			NewPermissions: new HashSet<Permission> { AccountRead },
			UpdatedBy: Guid.CreateVersion7()
		);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _roleRepository.DidNotReceive().ReplacePermissionsAsync(
			roleId: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlySet<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
