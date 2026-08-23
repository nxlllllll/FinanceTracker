using FinanceTracker.Api.Endpoints.Users.Commands;
using FinanceTracker.Api.Endpoints.Users.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Application.UseCases.Role.Commands.AssignRoleToUser;
using FinanceTracker.Application.UseCases.Role.Commands.RemoveRoleFromUser;
using FinanceTracker.Application.UseCases.User.Commands.ChangeUserBaseCurrency;
using FinanceTracker.Application.UseCases.User.Commands.ChangeUserEmail;
using FinanceTracker.Application.UseCases.User.Commands.ChangeUserPassword;
using FinanceTracker.Application.UseCases.UserPermission.Commands.GrantPermission;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;
using NSubstitute;
using UnitResult = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Tests.Unit.Api.Endpoints;

public sealed class UserEndpointTests
{
	private static readonly Guid CallerId = Guid.CreateVersion7();
	private static readonly Guid CallerSessionId = Guid.CreateVersion7();

	private static ICurrentUserProvider CurrentUser()
	{
		ICurrentUserProvider currentUser = Substitute.For<ICurrentUserProvider>();
		currentUser.UserId.Returns(returnThis: CallerId);
		currentUser.SessionId.Returns(returnThis: CallerSessionId);

		return currentUser;
	}

	private static ISender SenderReturning<TRequest, TValue>(Result<TValue, AppException> result)
		where TRequest : IRequest<Result<TValue, AppException>>
	{
		ISender sender = Substitute.For<ISender>();

		sender.Send(
			request: Arg.Any<TRequest>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: result);

		return sender;
	}

	private static Result<Guid, AppException> OkGuid() => Result<Guid, AppException>.Success(value: Guid.CreateVersion7());
	private static Result<UnitResult, AppException> OkUnit() => Result<UnitResult, AppException>.Success(value: UnitResult.Default);

	[Test]
	public async Task AssignRole_ShouldRecordTheCallerAsTheActorAndTheRouteUserAsTheTarget()
	{
		Guid targetUserId = Guid.CreateVersion7();
		Guid roleId = Guid.CreateVersion7();

		ISender sender = SenderReturning<AssignRoleToUserCommand, UnitResult>(result: OkUnit());

		await AssignRoleToUserEndpoint.HandleAsync(
			userId: targetUserId,
			roleId: roleId,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<AssignRoleToUserCommand>(predicate: command =>
				command!.UserId == targetUserId &&
				command.RoleId == roleId &&
				command.AssignedBy == CallerId
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task RemoveRole_ShouldRecordTheCallerAsTheActorAndTheRouteUserAsTheTarget()
	{
		Guid targetUserId = Guid.CreateVersion7();
		Guid roleId = Guid.CreateVersion7();

		ISender sender = SenderReturning<RemoveRoleFromUserCommand, UnitResult>(result: OkUnit());

		await RemoveRoleFromUserEndpoint.HandleAsync(
			userId: targetUserId,
			roleId: roleId,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<RemoveRoleFromUserCommand>(predicate: command =>
				command!.UserId == targetUserId &&
				command.RoleId == roleId &&
				command.RemovedBy == CallerId
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GrantPermission_WithAnUnparsablePermission_ShouldNotReachTheHandler()
	{
		ISender sender = Substitute.For<ISender>();

		await GrantPermissionEndpoint.HandleAsync(
			userId: Guid.CreateVersion7(),
			request: new GrantPermissionRequest(Permission: "everything"),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<GrantPermissionCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task GrantPermission_ShouldRecordTheCallerAsTheGrantorAndTheRouteUserAsTheTarget()
	{
		Guid targetUserId = Guid.CreateVersion7();

		ISender sender = SenderReturning<GrantPermissionCommand, UnitResult>(result: OkUnit());

		await GrantPermissionEndpoint.HandleAsync(
			userId: targetUserId,
			request: new GrantPermissionRequest(Permission: "account:write"),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GrantPermissionCommand>(predicate: command =>
				command!.TargetUserId == targetUserId &&
				command.GrantedBy == CallerId &&
				command.Permission.ToString() == "account:write"
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ChangeBaseCurrency_WithAnInvalidCode_ShouldNotReachTheHandler()
	{
		ISender sender = Substitute.For<ISender>();

		await ChangeBaseCurrencyEndpoint.HandleAsync(
			request: new ChangeBaseCurrencyRequest(BaseCurrency: "RUBLE"),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<ChangeUserBaseCurrencyCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task ChangeBaseCurrency_ShouldActOnTheCallerAlone()
	{
		ISender sender = SenderReturning<ChangeUserBaseCurrencyCommand, Guid>(result: OkGuid());

		await ChangeBaseCurrencyEndpoint.HandleAsync(
			request: new ChangeBaseCurrencyRequest(BaseCurrency: "USD"),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<ChangeUserBaseCurrencyCommand>(predicate: command =>
				command!.UserId == CallerId && command.NewBaseCurrency.Value == "USD"
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ChangeEmail_ShouldCarryTheCurrentSessionSoTheOthersCanBeRevoked()
	{
		ISender sender = SenderReturning<ChangeUserEmailCommand, Guid>(result: OkGuid());

		await ChangeEmailEndpoint.HandleAsync(
			request: new ChangeEmailRequest(CurrentPassword: "old-password", NewEmail: "new@test.com"),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<ChangeUserEmailCommand>(predicate: command =>
				command!.UserId == CallerId &&
				command.CurrentSessionId == CallerSessionId &&
				command.CurrentPassword == "old-password" &&
				command.NewEmail == "new@test.com"
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ChangeEmail_WithAMalformedAddress_ShouldStillReachTheHandler()
	{
		ISender sender = SenderReturning<ChangeUserEmailCommand, Guid>(result: OkGuid());

		await ChangeEmailEndpoint.HandleAsync(
			request: new ChangeEmailRequest(CurrentPassword: "old-password", NewEmail: "not-an-email"),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Any<ChangeUserEmailCommand>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ChangePassword_ShouldCarryTheCurrentSessionSoTheOthersCanBeRevoked()
	{
		ISender sender = SenderReturning<ChangeUserPasswordCommand, Guid>(result: OkGuid());

		await ChangePasswordEndpoint.HandleAsync(
			request: new ChangePasswordRequest(CurrentPassword: "old-password", NewPassword: "new-password"),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<ChangeUserPasswordCommand>(predicate: command =>
				command!.UserId == CallerId &&
				command.CurrentSessionId == CallerSessionId &&
				command.CurrentPassword == "old-password" &&
				command.NewPassword == "new-password"
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}
