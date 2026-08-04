using FinanceTracker.Application.UseCases.Role.Commands.DeleteRole;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Role;

public sealed class DeleteRoleHandlerTests
{
	private IRoleRepository _roleRepository = null!;
	private DeleteRoleHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_roleRepository = Substitute.For<IRoleRepository>();
		_handler = new DeleteRoleHandler(roleRepository: _roleRepository);
	}

	private static RoleDto BuildRole(Guid roleId) => new RoleDto(
		Id: roleId,
		SystemKey: null,
		DisplayName: Name.Create(value: "Test Role").Value!,
		Permissions: new HashSet<Permission>
		{
			Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!
		}
	);

	private void ReturnsMembers(
		Guid roleId,
		params Guid[] userIds
	) => _roleRepository.GetMemberUserIdsAsync(
		roleId: roleId,
		ct: Arg.Any<CancellationToken>()
	).Returns(returnThis: userIds.ToList());

	[Test]
	public async Task HandleAsync_WithNoMembers_ShouldDeleteTheRole()
	{
		Guid roleId = Guid.CreateVersion7();
		ReturnsMembers(roleId: roleId);

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.HandleAsync(
			request: new DeleteRoleCommand(RoleId: roleId, DeletedBy: Guid.CreateVersion7()),
			role: BuildRole(roleId: roleId)
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _roleRepository.Received(requiredNumberOfCalls: 1).DeleteAsync(roleId: roleId, ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task HandleAsync_WithMembers_ShouldRefuseAndDeleteNothing()
	{
		Guid roleId = Guid.CreateVersion7();
		ReturnsMembers(roleId: roleId, Guid.CreateVersion7(), Guid.CreateVersion7());

		Result<FinanceTracker.Core.Results.Unit, AppException> result = await _handler.HandleAsync(
			request: new DeleteRoleCommand(RoleId: roleId, DeletedBy: Guid.CreateVersion7()),
			role: BuildRole(roleId: roleId)
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<RoleHasMembersException>();
		await Assert.That(value: ((RoleHasMembersException)result.Error!).MemberCount).IsEqualTo(expected: 2);

		await _roleRepository.DidNotReceive().DeleteAsync(roleId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>());
	}
}
