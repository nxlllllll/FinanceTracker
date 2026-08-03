using FinanceTracker.Application.UseCases.Role.Commands.UpdateRolePermissions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Role;

public sealed class UpdateRolePermissionsHandlerTests
{
	private static readonly Permission AccountRead = Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!;
	private static readonly Permission CategoryRead = Permission.Create(resource: Resource.Category, action: PermissionAction.Read).Value!;

	private IRoleRepository _roleRepository = null!;
	private UpdateRolePermissionsHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_roleRepository = Substitute.For<IRoleRepository>();
		_handler = new UpdateRolePermissionsHandler(roleRepository: _roleRepository);
	}

	private static RoleDto BuildRole(Guid roleId, params Permission[] permissions) => new RoleDto(
		Id: roleId,
		SystemKey: null,
		DisplayName: Name.Create(value: "Test Role").Value!,
		Permissions: permissions.ToHashSet()
	);

	[Test]
	public async Task HandleAsync_ShouldReplaceTheRolePermissionSet()
	{
		Guid roleId = Guid.CreateVersion7();
		RoleDto role = BuildRole(roleId: roleId, AccountRead);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.HandleAsync(
			request: new UpdateRolePermissionsCommand(
				RoleId: roleId,
				NewPermissions: new HashSet<Permission> { CategoryRead },
				UpdatedBy: Guid.CreateVersion7()
			),
			role: role
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _roleRepository.Received(requiredNumberOfCalls: 1).ReplacePermissionsAsync(
			roleId: roleId,
			permissions: Arg.Is<IReadOnlySet<Permission>>(predicate: p => p!.Contains(item: CategoryRead) && !p.Contains(item: AccountRead)),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_ShouldNotTouchTheMembers()
	{
		Guid roleId = Guid.CreateVersion7();

		await _handler.HandleAsync(
			request: new UpdateRolePermissionsCommand(
				RoleId: roleId,
				NewPermissions: new HashSet<Permission> { CategoryRead },
				UpdatedBy: Guid.CreateVersion7()
			),
			role: BuildRole(roleId: roleId, AccountRead)
		);

		await _roleRepository.DidNotReceive().GetMemberUserIdsAsync(
			roleId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithAnUnchangedPermissionSet_ShouldDoNothing()
	{
		Guid roleId = Guid.CreateVersion7();

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.HandleAsync(
			request: new UpdateRolePermissionsCommand(
				RoleId: roleId,
				NewPermissions: new HashSet<Permission> { AccountRead },
				UpdatedBy: Guid.CreateVersion7()
			),
			role: BuildRole(roleId: roleId, AccountRead)
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _roleRepository.DidNotReceive().ReplacePermissionsAsync(
			roleId: Arg.Any<Guid>(),
			permissions: Arg.Any<IReadOnlySet<Permission>>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
