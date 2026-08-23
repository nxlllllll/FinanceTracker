using FinanceTracker.Application.UseCases.Role.Authorization;
using FinanceTracker.Application.UseCases.Role.Commands.DeleteRole;
using FinanceTracker.Application.UseCases.Role.Commands.UpdateRolePermissions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Role;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Loaders;

public sealed class RoleLoaderTests
{
	private IRoleRepository _roleRepository = null!;
	private RoleLoader _loader = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_roleRepository = Substitute.For<IRoleRepository>();
		_loader = new RoleLoader(roleRepository: _roleRepository);
	}

	private static RoleDto BuildRole(
		Guid roleId,
		SystemRole? systemKey = null
	) => new RoleDto(
		Id: roleId,
		SystemKey: systemKey,
		DisplayName: Name.Create(value: "Test Role").Value!,
		Permissions: new HashSet<Permission>()
	);

	private void ReturnsRole(
		Guid roleId,
		RoleDto? role
	) => _roleRepository.GetByIdAsync(
		roleId: roleId,
		ct: Arg.Any<CancellationToken>()
	).Returns(returnThis: role);

	[Test]
	public async Task LoadAsync_ForDelete_WhenRoleNotFound_ShouldReturnNotFound()
	{
		Guid roleId = Guid.CreateVersion7();
		ReturnsRole(roleId: roleId, role: null);

		Result<RoleDto, AppException> result = await _loader.LoadAsync(
			request: new DeleteRoleCommand(RoleId: roleId, DeletedBy: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_ForDelete_WhenRoleIsSystem_ShouldRefuse()
	{
		Guid roleId = Guid.CreateVersion7();
		ReturnsRole(roleId: roleId, role: BuildRole(roleId: roleId, systemKey: SystemRole.User));

		Result<RoleDto, AppException> result = await _loader.LoadAsync(
			request: new DeleteRoleCommand(RoleId: roleId, DeletedBy: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<CannotDeleteSystemRoleException>();
	}

	[Test]
	public async Task LoadAsync_ForDelete_WhenRoleIsCustom_ShouldReturnIt()
	{
		Guid roleId = Guid.CreateVersion7();
		ReturnsRole(roleId: roleId, role: BuildRole(roleId: roleId));

		Result<RoleDto, AppException> result = await _loader.LoadAsync(
			request: new DeleteRoleCommand(RoleId: roleId, DeletedBy: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value!.Id).IsEqualTo(expected: roleId);
	}

	[Test]
	public async Task LoadAsync_ForUpdate_WhenRoleNotFound_ShouldReturnNotFound()
	{
		Guid roleId = Guid.CreateVersion7();
		ReturnsRole(roleId: roleId, role: null);

		Result<RoleDto, AppException> result = await _loader.LoadAsync(
			request: new UpdateRolePermissionsCommand(
				RoleId: roleId,
				NewPermissions: new HashSet<Permission>(),
				UpdatedBy: Guid.CreateVersion7()
			),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_ForUpdate_WhenRoleIsSystem_ShouldReturnIt()
	{
		Guid roleId = Guid.CreateVersion7();
		ReturnsRole(roleId: roleId, role: BuildRole(roleId: roleId, systemKey: SystemRole.User));

		Result<RoleDto, AppException> result = await _loader.LoadAsync(
			request: new UpdateRolePermissionsCommand(
				RoleId: roleId,
				NewPermissions: new HashSet<Permission>(),
				UpdatedBy: Guid.CreateVersion7()
			),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
	}
}
