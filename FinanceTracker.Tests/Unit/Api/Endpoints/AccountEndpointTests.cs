using FinanceTracker.Api.Endpoints.Accounts.Commands;
using FinanceTracker.Api.Endpoints.Accounts.Contracts;
using FinanceTracker.Api.Endpoints.Accounts.Queries;
using FinanceTracker.Api.Http;
using FinanceTracker.Application.UseCases.Account.Commands.ArchiveAccount;
using FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;
using FinanceTracker.Application.UseCases.Account.Commands.RenameAccount;
using FinanceTracker.Application.UseCases.Account.Commands.UnarchiveAccount;
using FinanceTracker.Application.UseCases.Account.Queries.GetAccount;
using FinanceTracker.Application.UseCases.Account.Queries.GetAccounts;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels.Account;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Api.Endpoints;

public sealed class AccountEndpointTests
{
	private static readonly Guid CallerId = Guid.CreateVersion7();

	private static ICurrentUserProvider CurrentUser()
	{
		ICurrentUserProvider currentUser = Substitute.For<ICurrentUserProvider>();
		currentUser.UserId.Returns(returnThis: CallerId);

		return currentUser;
	}

	private static HttpContext Context(
		Stream body,
		string? ifMatch = null,
		Guid? idempotencyKey = null)
	{
		HttpContext context = HttpContextFactory.Create(body: body, method: "PATCH", path: "/api/v1/accounts");

		if (ifMatch is not null)
			context.Request.Headers.IfMatch = ifMatch;

		if (idempotencyKey is not null)
			context.Request.Headers["Idempotency-Key"] = idempotencyKey.Value.ToString();

		return context;
	}

	private static ISender SenderReturning<TRequest>(
		Result<Guid, AppException> result
	) where TRequest : IRequest<Result<Guid, AppException>>
	{
		ISender sender = Substitute.For<ISender>();

		sender.Send(
			request: Arg.Any<TRequest>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: result);

		return sender;
	}

	private static Result<Guid, AppException> Ok() => Result<Guid, AppException>.Success(value: Guid.CreateVersion7());


	private static ISender SenderForListing()
	{
		ISender sender = Substitute.For<ISender>();

		sender.Send(
			request: Arg.Any<GetAccountsQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<IReadOnlyList<AccountReadModel>, AppException>.Success(value: []));

		return sender;
	}

	private static CreateAccountRequest CreateRequest(
		string name = "Основной",
		string currency = "RUB",
		decimal initialBalance = 0m
	) => new CreateAccountRequest(
		Name: name,
		Type: AccountType.Checking,
		Currency: currency,
		InitialBalance: initialBalance
	);

	[Test]
	public async Task Archive_WithAnIfMatchHeader_ShouldCarryTheVersionIntoTheCommand()
	{
		using MemoryStream body = new MemoryStream();
		Guid accountId = Guid.CreateVersion7();

		ISender sender = SenderReturning<ArchiveAccountCommand>(result: Ok());

		await ArchiveAccountEndpoint.HandleAsync(
			accountId: accountId,
			httpContext: Context(body: body, ifMatch: "\"7\""),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<ArchiveAccountCommand>(predicate: command =>
				command!.UserId == CallerId &&
				command.AccountId == accountId &&
				command.ExpectedVersion == 7
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Archive_WithoutAnIfMatchHeader_ShouldNotClaimAVersion()
	{
		using MemoryStream body = new MemoryStream();

		ISender sender = SenderReturning<ArchiveAccountCommand>(result: Ok());

		await ArchiveAccountEndpoint.HandleAsync(
			accountId: Guid.CreateVersion7(),
			httpContext: Context(body: body),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<ArchiveAccountCommand>(predicate: command => command!.ExpectedVersion == null),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Unarchive_WithAnIfMatchHeader_ShouldCarryTheVersionIntoTheCommand()
	{
		using MemoryStream body = new MemoryStream();
		Guid accountId = Guid.CreateVersion7();

		ISender sender = SenderReturning<UnarchiveAccountCommand>(result: Ok());

		await UnarchiveAccountEndpoint.HandleAsync(
			accountId: accountId,
			httpContext: Context(body: body, ifMatch: "\"3\""),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<UnarchiveAccountCommand>(predicate: command =>
				command!.UserId == CallerId &&
				command.AccountId == accountId &&
				command.ExpectedVersion == 3
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Rename_WithAnInvalidName_ShouldNotReachTheHandler()
	{
		using MemoryStream body = new MemoryStream();

		ISender sender = Substitute.For<ISender>();

		await RenameAccountEndpoint.HandleAsync(
			accountId: Guid.CreateVersion7(),
			request: new RenameAccountRequest(NewName: "   "),
			httpContext: Context(body: body),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<RenameAccountCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Rename_WithAValidName_ShouldCarryBothTheNameAndTheVersion()
	{
		using MemoryStream body = new MemoryStream();
		Guid accountId = Guid.CreateVersion7();

		ISender sender = SenderReturning<RenameAccountCommand>(result: Ok());

		await RenameAccountEndpoint.HandleAsync(
			accountId: accountId,
			request: new RenameAccountRequest(NewName: "Копилка"),
			httpContext: Context(body: body, ifMatch: "\"12\""),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<RenameAccountCommand>(predicate: command =>
				command!.UserId == CallerId &&
				command.AccountId == accountId &&
				command.NewName.Value == "Копилка" &&
				command.ExpectedVersion == 12
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Create_WithAnInvalidName_ShouldNotReachTheHandler()
	{
		using MemoryStream body = new MemoryStream();

		ISender sender = Substitute.For<ISender>();

		await CreateAccountEndpoint.HandleAsync(
			request: CreateRequest(name: "   "),
			linkGenerator: Substitute.For<LinkGenerator>(),
			currentUser: CurrentUser(),
			sender: sender,
			httpContext: Context(body: body, idempotencyKey: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<CreateAccountCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Create_WithAnInvalidCurrency_ShouldNotReachTheHandler()
	{
		using MemoryStream body = new MemoryStream();

		ISender sender = Substitute.For<ISender>();

		await CreateAccountEndpoint.HandleAsync(
			request: CreateRequest(currency: "RUBLE"),
			linkGenerator: Substitute.For<LinkGenerator>(),
			currentUser: CurrentUser(),
			sender: sender,
			httpContext: Context(body: body, idempotencyKey: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<CreateAccountCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Create_WithAValidRequest_ShouldSendACommandBuiltFromIt()
	{
		using MemoryStream body = new MemoryStream();
		Guid idempotencyKey = Guid.CreateVersion7();

		ISender sender = SenderReturning<CreateAccountCommand>(result: Ok());

		await CreateAccountEndpoint.HandleAsync(
			request: CreateRequest(name: "Основной", currency: "RUB", initialBalance: 5_000m),
			linkGenerator: Substitute.For<LinkGenerator>(),
			currentUser: CurrentUser(),
			sender: sender,
			httpContext: Context(body: body, idempotencyKey: idempotencyKey),
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<CreateAccountCommand>(predicate: command =>
				command!.UserId == CallerId &&
				command.Name.Value == "Основной" &&
				command.Currency.Value == "RUB" &&
				command.InitialBalance == 5_000m &&
				command.IdempotencyKey == idempotencyKey
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Create_WithoutAnIdempotencyKey_ShouldStillSendACommandWithAnEmptyKey()
	{
		using MemoryStream body = new MemoryStream();

		ISender sender = SenderReturning<CreateAccountCommand>(result: Ok());

		await CreateAccountEndpoint.HandleAsync(
			request: CreateRequest(),
			linkGenerator: Substitute.For<LinkGenerator>(),
			currentUser: CurrentUser(),
			sender: sender,
			httpContext: Context(body: body),
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<CreateAccountCommand>(predicate: command => command!.IdempotencyKey == Guid.Empty),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetAccount_ShouldQueryForTheCallersOwnRecord()
	{
		Guid accountId = Guid.CreateVersion7();

		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<GetAccountQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<AccountReadModel, AppException>.Failure(
			error: new NotFoundException(message: "Account not found.", id: accountId)
		));

		await GetAccountEndpoint.HandleAsync(
			accountId: accountId,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetAccountQuery>(predicate: query =>
				query!.AccountId == accountId && query.UserId == CallerId
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetAccounts_WithoutAFilter_ShouldNotConstrainByArchivedState()
	{
		ISender sender = SenderForListing();

		await GetAccountsEndpoint.HandleAsync(
			isArchived: null,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetAccountsQuery>(predicate: query =>
				query!.UserId == CallerId && query.IsArchived == null
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetAccounts_WithAFilter_ShouldPassItThrough()
	{
		ISender sender = SenderForListing();

		await GetAccountsEndpoint.HandleAsync(
			isArchived: true,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetAccountsQuery>(predicate: query => query!.IsArchived == true),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}
