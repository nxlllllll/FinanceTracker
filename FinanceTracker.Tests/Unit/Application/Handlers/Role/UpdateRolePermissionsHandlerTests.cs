using FinanceTracker.Application.UseCases.Role.Commands.UpdateRolePermissions;
using FinanceTracker.Application.UseCases.UserPermission.Commands.GrantPermission;
using FinanceTracker.Application.UseCases.UserPermission.Commands.RevokePermission;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Role;

public sealed class UpdateRolePermissionsHandlerTests
{
	private IRoleRepository _roleRepository = null!;
	private ISender _sender = null!;
	private UpdateRolePermissionsHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_roleRepository = Substitute.For<IRoleRepository>();
		_sender = Substitute.For<ISender>();
		_sender.Send(
			request: Arg.Any<GrantPermissionCommand>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Success(value: FinanceTracker.Core.Results.Unit.Default));
		_sender.Send(
			request: Arg.Any<RevokePermissionCommand>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Success(value: FinanceTracker.Core.Results.Unit.Default));

		_handler = new UpdateRolePermissionsHandler(roleRepository: _roleRepository, sender: _sender);
	}

	private static RoleDto BuildRole(Guid roleId, params Permission[] permissions) => new RoleDto(
		Id: roleId,
		SystemKey: null,
		DisplayName: Name.Create(value: "Test Role").Value!,
		Permissions: permissions.ToHashSet()
	);

	[Test]
	public async Task Handle_WhenRoleNotFound_ShouldReturnFailure()
	{
		Guid roleId = Guid.CreateVersion7();
		_roleRepository.GetByIdAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (RoleDto?)null);

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
		Permission accountRead = Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!;
		Permission budgetWrite = Permission.Create(resource: Resource.Budget, action: PermissionAction.Write).Value!;

		_roleRepository.GetByIdAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: BuildRole(roleId: roleId, accountRead));
		_roleRepository.GetMemberUserIdsAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new List<Guid> { memberUserId });

		UpdateRolePermissionsCommand command = new UpdateRolePermissionsCommand(
			RoleId: roleId,
			NewPermissions: new HashSet<Permission> { accountRead, budgetWrite },
			UpdatedBy: Guid.CreateVersion7()
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GrantPermissionCommand>(predicate: c => c!.TargetUserId == memberUserId && c.Permission == budgetWrite),
			cancellationToken: Arg.Any<CancellationToken>()
		);
		await _sender.DidNotReceive().Send(
			request: Arg.Is<GrantPermissionCommand>(predicate: c => c!.Permission == accountRead),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_ShouldRevokeOnlyRemovedPermissionsFromExistingMembers()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid memberUserId = Guid.CreateVersion7();
		Permission accountRead = Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!;
		Permission budgetWrite = Permission.Create(resource: Resource.Budget, action: PermissionAction.Write).Value!;

		_roleRepository.GetByIdAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: BuildRole(roleId: roleId, accountRead, budgetWrite));
		_roleRepository.GetMemberUserIdsAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new List<Guid> { memberUserId });

		UpdateRolePermissionsCommand command = new UpdateRolePermissionsCommand(
			RoleId: roleId,
			NewPermissions: new HashSet<Permission> { accountRead },
			UpdatedBy: Guid.CreateVersion7()
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<RevokePermissionCommand>(predicate: c => c!.TargetUserId == memberUserId && c.Permission == budgetWrite),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_ShouldReplacePermissionsInRepository()
	{
		Guid roleId = Guid.CreateVersion7();
		_roleRepository.GetByIdAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: BuildRole(roleId: roleId));
		_roleRepository.GetMemberUserIdsAsync(
			roleId: roleId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new List<Guid>());

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

		await _roleRepository.Received(requiredNumberOfCalls: 1).ReplacePermissionsAsync(roleId: roleId, permissions: newPermissions, ct: Arg.Any<CancellationToken>());
	}
}
