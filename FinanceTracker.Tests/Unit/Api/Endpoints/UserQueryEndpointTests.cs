using FinanceTracker.Api.Endpoints.Users.Commands;
using FinanceTracker.Api.Endpoints.Users.Queries;
using FinanceTracker.Api.Http;
using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.UseCases.Role.Queries.GetUserRoles;
using FinanceTracker.Application.UseCases.User.Queries.GetIncomeExpenseSummary;
using FinanceTracker.Application.UseCases.User.Queries.GetOperationsHistory;
using FinanceTracker.Application.UseCases.User.Queries.GetTotalBalance;
using FinanceTracker.Application.UseCases.User.Queries.GetUser;
using FinanceTracker.Application.UseCases.UserPermission.Commands.RevokePermission;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Operation;
using FinanceTracker.Core.ReadModels.User;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using NSubstitute;
using UnitResult = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Tests.Unit.Api.Endpoints;

public sealed class UserQueryEndpointTests
{
	private static readonly Guid CallerId = Guid.CreateVersion7();

	private static ICurrentUserProvider CurrentUser()
	{
		ICurrentUserProvider currentUser = Substitute.For<ICurrentUserProvider>();
		currentUser.UserId.Returns(returnThis: CallerId);

		return currentUser;
	}


	private static ISender SenderForHistory()
	{
		ISender sender = Substitute.For<ISender>();

		sender.Send(
			request: Arg.Any<GetOperationsHistoryQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<PagedResult<Operation>, AppException>.Success(value: new PagedResult<Operation>(
			Items: [],
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		)));

		return sender;
	}

	[Test]
	public async Task RevokePermission_WithAnUnparsablePermission_ShouldNotReachTheHandler()
	{
		ISender sender = Substitute.For<ISender>();

		await RevokePermissionEndpoint.HandleAsync(
			userId: Guid.CreateVersion7(),
			permission: "everything",
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<RevokePermissionCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task RevokePermission_ShouldRecordTheCallerAsTheActorAndTheRouteUserAsTheTarget()
	{
		Guid targetUserId = Guid.CreateVersion7();

		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<RevokePermissionCommand>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<UnitResult, AppException>.Success(value: UnitResult.Default));

		await RevokePermissionEndpoint.HandleAsync(
			userId: targetUserId,
			permission: "account:write",
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<RevokePermissionCommand>(predicate: command =>
				command!.TargetUserId == targetUserId &&
				command.RevokedBy == CallerId &&
				command.Permission.ToString() == "account:write"
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetMe_ShouldReadWhoeverTheTokenBelongsTo()
	{
		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<GetUserQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<UserReadModel, AppException>.Failure(
			error: new NotFoundException(message: "User not found.", id: CallerId)
		));

		await GetMeEndpoint.HandleAsync(
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetUserQuery>(predicate: query => query!.UserId == CallerId),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetTotalBalance_ShouldReadTheCallersOwnTotal()
	{
		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<GetTotalBalanceQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<TotalBalanceReadModel, AppException>.Failure(
			error: new NotFoundException(message: "User not found.", id: CallerId)
		));

		await GetTotalBalanceEndpoint.HandleAsync(
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetTotalBalanceQuery>(predicate: query => query!.UserId == CallerId),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetIncomeExpenseSummary_ShouldPassThePeriodThroughUnchanged()
	{
		DateOnly period = new DateOnly(year: 2026, month: 9, day: 17);

		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<GetIncomeExpenseSummaryQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<IncomeExpenseSummary, AppException>.Failure(
			error: new NotFoundException(message: "User not found.", id: CallerId)
		));

		await GetIncomeExpenseSummaryEndpoint.HandleAsync(
			period: period,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetIncomeExpenseSummaryQuery>(predicate: query =>
				query!.UserId == CallerId && query.Period == period
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetUserRoles_ShouldQueryForTheRouteUser()
	{
		Guid targetUserId = Guid.CreateVersion7();

		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<GetUserRolesQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<IReadOnlyList<RoleDto>, AppException>.Success(value: []));

		await GetUserRolesEndpoint.HandleAsync(
			userId: targetUserId,
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetUserRolesQuery>(predicate: query => query!.UserId == targetUserId),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetOperationsHistory_WithAnUnparsableType_ShouldNotReachTheHandler()
	{
		ISender sender = Substitute.For<ISender>();

		await GetOperationsHistoryEndpoint.HandleAsync(
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None,
			type: "neither"
		);

		await sender.DidNotReceive().Send(request: Arg.Any<GetOperationsHistoryQuery>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task GetOperationsHistory_WithoutFilters_ShouldConstrainNothingButTheOwner()
	{
		ISender sender = SenderForHistory();

		await GetOperationsHistoryEndpoint.HandleAsync(
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetOperationsHistoryQuery>(predicate: query =>
				query!.UserId == CallerId &&
				query.Type == null &&
				query.DateFrom == null &&
				query.DateTo == null
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetOperationsHistory_ShouldNormaliseEveryInstantToUtc()
	{
		DateTimeOffset from = new DateTimeOffset(year: 2026, month: 9, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.FromHours(value: 12));
		DateTimeOffset to = new DateTimeOffset(year: 2026, month: 9, day: 30, hour: 0, minute: 0, second: 0, offset: TimeSpan.FromHours(value: -10));
		DateTimeOffset cursor = new DateTimeOffset(year: 2026, month: 9, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.FromHours(value: 3));

		ISender sender = SenderForHistory();

		await GetOperationsHistoryEndpoint.HandleAsync(
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None,
			dateFrom: from,
			dateTo: to,
			cursorOccurredAt: cursor
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetOperationsHistoryQuery>(predicate: query =>
				query!.DateFrom == from.ToUniversalTime() &&
				query.DateTo == to.ToUniversalTime() &&
				query.CursorOccurredAt == cursor.ToUniversalTime()
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetOperationsHistory_WithATypeInAnyCasing_ShouldParseIt()
	{
		ISender sender = SenderForHistory();

		await GetOperationsHistoryEndpoint.HandleAsync(
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None,
			type: "tRaNsFeR"
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetOperationsHistoryQuery>(predicate: query => query!.Type == OperationFilterType.Transfer),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}
