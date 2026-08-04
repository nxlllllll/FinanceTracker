using FinanceTracker.Application.Services.Roles;
using FinanceTracker.Cli.Commands;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Cli;

public sealed class GrantRootCommandTests
{
	private const string Email = "admin@example.com";

	private IUserAuthRepository _userAuthRepository = null!;
	private IRoleRepository _roleRepository = null!;
	private IUserRoleService _userRoleService = null!;
	private GrantRootCommand _command = null!;

	private static readonly RoleDto RootRole = new RoleDto(
		Id: Guid.CreateVersion7(),
		SystemKey: SystemRole.Root,
		DisplayName: Name.Reconstitute(value: "root"),
		Permissions: new HashSet<Permission>()
	);

	[Before(hookType: Test)]
	public void Setup()
	{
		_userAuthRepository = Substitute.For<IUserAuthRepository>();
		_roleRepository = Substitute.For<IRoleRepository>();
		_userRoleService = Substitute.For<IUserRoleService>();

		_roleRepository.GetBySystemKeyAsync(
			systemKey: SystemRole.Root,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RootRole);

		_userRoleService.AssignAsync(
			userId: Arg.Any<Guid>(),
			roleId: Arg.Any<Guid>(),
			assignedBy: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, AppException>.Success(value: FinanceTracker.Core.Results.Unit.Default));

		_command = new GrantRootCommand(
			userAuthRepository: _userAuthRepository,
			roleRepository: _roleRepository,
			userRoleService: _userRoleService,
			logger: NullLogger<GrantRootCommand>.Instance
		);
	}

	private void ReturnsUser(User? user) => _userAuthRepository.GetByEmailAsync(
		email: Email,
		ct: Arg.Any<CancellationToken>()
	).Returns(returnThis: user);

	[Test]
	public async Task ExecuteAsync_ShouldAssignThroughTheServiceRatherThanWritingTheProjection()
	{
		User user = UserFactory.Create().Value!;
		ReturnsUser(user: user);

		int exitCode = await _command.ExecuteAsync(email: Email);

		await Assert.That(value: exitCode).IsEqualTo(expected: 0);
		await _userRoleService.Received(requiredNumberOfCalls: 1).AssignAsync(
			userId: user.Id,
			roleId: RootRole.Id,
			assignedBy: SystemActor.Id,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ExecuteAsync_WithAnUnknownEmail_ShouldFailWithoutAssigningAnything()
	{
		ReturnsUser(user: null);

		int exitCode = await _command.ExecuteAsync(email: Email);

		await Assert.That(value: exitCode).IsEqualTo(expected: 1).Because(message: """
			A non-zero exit is what makes a deployment step fail loudly. Reporting success here would leave
			an environment with no administrator and nothing to say so.
		""");
		await _userRoleService.DidNotReceive().AssignAsync(
			userId: Arg.Any<Guid>(),
			roleId: Arg.Any<Guid>(),
			assignedBy: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ExecuteAsync_WithNoRootRoleSeeded_ShouldFail()
	{
		ReturnsUser(user: UserFactory.Create().Value);
		_roleRepository.GetBySystemKeyAsync(
			systemKey: SystemRole.Root,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<RoleDto?>(result: null));

		await Assert.That(value: await _command.ExecuteAsync(email: Email)).IsEqualTo(expected: 1);
	}
}
