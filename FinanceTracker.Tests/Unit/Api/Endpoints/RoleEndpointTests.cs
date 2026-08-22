using FinanceTracker.Api.Endpoints.Roles.Commands;
using FinanceTracker.Api.Endpoints.Roles.Contracts;
using FinanceTracker.Api.Endpoints.Roles.Queries;
using FinanceTracker.Api.Http;
using FinanceTracker.Application.UseCases.Role.Commands.CreateRole;
using FinanceTracker.Application.UseCases.Role.Commands.DeleteRole;
using FinanceTracker.Application.UseCases.Role.Commands.UpdateRolePermissions;
using FinanceTracker.Application.UseCases.Role.Queries.GetRole;
using FinanceTracker.Application.UseCases.Role.Queries.GetRoles;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using UnitResult = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Tests.Unit.Api.Endpoints;

public class RoleEndpointTests
{
	private static readonly Guid CallerId = Guid.CreateVersion7();

	private static ICurrentUserProvider CurrentUser()
	{
		ICurrentUserProvider currentUser = Substitute.For<ICurrentUserProvider>();
		currentUser.UserId.Returns(returnThis: CallerId);

		return currentUser;
	}

	private static HttpContext Context(Stream body, Guid? idempotencyKey = null)
	{
		HttpContext context = HttpContextFactory.Create(body: body, method: "POST", path: "/api/v1/roles");

		if (idempotencyKey is not null)
			context.Request.Headers["Idempotency-Key"] = idempotencyKey.Value.ToString();

		return context;
	}

	private static Result<UnitResult, AppException> OkUnit() => Result<UnitResult, AppException>.Success(value: UnitResult.Default);

	[Test]
	public async Task CreateRole_WithAnInvalidDisplayName_ShouldNotReachTheHandler()
	{
		using MemoryStream body = new MemoryStream();

		ISender sender = Substitute.For<ISender>();

		await CreateRoleEndpoint.HandleAsync(
			request: new CreateRoleRequest(DisplayName: "   ", Permissions: ["account:read"]),
			linkGenerator: Substitute.For<LinkGenerator>(),
			sender: sender,
			httpContext: Context(body: body, idempotencyKey: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<CreateRoleCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task CreateRole_WithAnUnparsablePermission_ShouldNotReachTheHandler()
	{
		using MemoryStream body = new MemoryStream();

		ISender sender = Substitute.For<ISender>();

		await CreateRoleEndpoint.HandleAsync(
			request: new CreateRoleRequest(DisplayName: "Бухгалтер", Permissions: ["account:read", "everything"]),
			linkGenerator: Substitute.For<LinkGenerator>(),
			sender: sender,
			httpContext: Context(body: body, idempotencyKey: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<CreateRoleCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task CreateRole_WithAValidRequest_ShouldSendACommandBuiltFromIt()
	{
		using MemoryStream body = new MemoryStream();

		Guid idempotencyKey = Guid.CreateVersion7();

		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<CreateRoleCommand>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<Guid, AppException>.Success(value: Guid.CreateVersion7()));

		await CreateRoleEndpoint.HandleAsync(
			request: new CreateRoleRequest(DisplayName: "Бухгалтер", Permissions: ["account:read", "transaction:read"]),
			linkGenerator: Substitute.For<LinkGenerator>(),
			sender: sender,
			httpContext: Context(body: body, idempotencyKey: idempotencyKey),
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<CreateRoleCommand>(predicate: command =>
				command!.DisplayName.Value == "Бухгалтер" &&
				command.Permissions.Count == 2 &&
				command.IdempotencyKey == idempotencyKey
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task CreateRole_WithoutAnIdempotencyKey_ShouldStillSendACommandWithAnEmptyKey()
	{
		using MemoryStream body = new MemoryStream();

		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<CreateRoleCommand>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<Guid, AppException>.Success(value: Guid.CreateVersion7()));

		await CreateRoleEndpoint.HandleAsync(
			request: new CreateRoleRequest(DisplayName: "Бухгалтер", Permissions: ["account:read"]),
			linkGenerator: Substitute.For<LinkGenerator>(),
			sender: sender,
			httpContext: Context(body: body),
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<CreateRoleCommand>(predicate: command => command!.IdempotencyKey == Guid.Empty),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task DeleteRole_ShouldRecordTheCallerAsTheActor()
	{
		Guid roleId = Guid.CreateVersion7();

		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<DeleteRoleCommand>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: OkUnit());

		await DeleteRoleEndpoint.HandleAsync(
			roleId: roleId,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<DeleteRoleCommand>(predicate: command =>
				command!.RoleId == roleId && command.DeletedBy == CallerId
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task UpdateRolePermissions_WithAnUnparsablePermission_ShouldNotReachTheHandler()
	{
		ISender sender = Substitute.For<ISender>();

		await UpdateRolePermissionsEndpoint.HandleAsync(
			roleId: Guid.CreateVersion7(),
			request: new UpdateRolePermissionsRequest(Permissions: ["account:read", "everything"]),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<UpdateRolePermissionsCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task UpdateRolePermissions_WithAnEmptySet_ShouldStillReachTheHandler()
	{
		Guid roleId = Guid.CreateVersion7();

		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<UpdateRolePermissionsCommand>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: OkUnit());

		await UpdateRolePermissionsEndpoint.HandleAsync(
			roleId: roleId,
			request: new UpdateRolePermissionsRequest(Permissions: []),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<UpdateRolePermissionsCommand>(predicate: command =>
				command!.RoleId == roleId &&
				command.UpdatedBy == CallerId &&
				command.NewPermissions.Count == 0
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task UpdateRolePermissions_ShouldRecordTheCallerAsTheActor()
	{
		Guid roleId = Guid.CreateVersion7();

		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<UpdateRolePermissionsCommand>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: OkUnit());

		await UpdateRolePermissionsEndpoint.HandleAsync(
			roleId: roleId,
			request: new UpdateRolePermissionsRequest(Permissions: ["account:read", "account:write"]),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<UpdateRolePermissionsCommand>(predicate: command =>
				command!.RoleId == roleId &&
				command.UpdatedBy == CallerId &&
				command.NewPermissions.Count == 2
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetRole_ShouldQueryByIdAlone()
	{
		Guid roleId = Guid.CreateVersion7();

		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<GetRoleQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<RoleDto, AppException>.Failure(
			error: new NotFoundException(message: "Role not found.", id: roleId)
		));

		await GetRoleEndpoint.HandleAsync(
			roleId: roleId,
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetRoleQuery>(predicate: query => query!.RoleId == roleId),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetRoles_ShouldQueryForAllOfThem()
	{
		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<GetRolesQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<IReadOnlyList<RoleDto>, AppException>.Success(value: []));

		await GetRolesEndpoint.HandleAsync(sender: sender, ct: CancellationToken.None);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Any<GetRolesQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}
