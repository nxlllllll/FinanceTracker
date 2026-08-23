using FinanceTracker.Api.Endpoints.RecurringTransactions.Commands;
using FinanceTracker.Api.Endpoints.RecurringTransactions.Contracts;
using FinanceTracker.Api.Endpoints.RecurringTransactions.Queries;
using FinanceTracker.Api.Http;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ActivateRecurringTransaction;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionAmount;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionCurrency;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionDayOfMonth;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.CreateRecurringTransaction;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.DeactivateRecurringTransaction;
using FinanceTracker.Application.UseCases.RecurringTransaction.Queries.GetRecurringTransaction;
using FinanceTracker.Application.UseCases.RecurringTransaction.Queries.GetRecurringTransactions;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Api.Endpoints;

public sealed class RecurringTransactionEndpointTests
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
		HttpContext context = HttpContextFactory.Create(body: body, method: "POST", path: "/api/v1/recurring-transactions");

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
			request: Arg.Any<GetRecurringTransactionsQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<PagedResult<RecurringTransactionReadModel>, AppException>.Success(value: new PagedResult<RecurringTransactionReadModel>(
			Items: [],
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		)));

		return sender;
	}

	private static CreateRecurringTransactionRequest CreateRequest(
		Guid? accountId = null,
		Guid? categoryId = null,
		decimal amount = 5_000m,
		string currency = "RUB",
		DirectionType direction = DirectionType.Debit,
		int dayOfMonth = 15,
		string? description = null
	) => new CreateRecurringTransactionRequest(
		AccountId: accountId ?? Guid.CreateVersion7(),
		CategoryId: categoryId ?? Guid.CreateVersion7(),
		Amount: amount,
		Currency: currency,
		Direction: direction,
		DayOfMonth: dayOfMonth,
		Description: description
	);

	[Test]
	public async Task Activate_ShouldAttributeTheCommandToTheCaller()
	{
		Guid recurringTransactionId = Guid.CreateVersion7();

		ISender sender = SenderReturning<ActivateRecurringTransactionCommand>(result: Ok());

		await ActivateRecurringTransactionEndpoint.HandleAsync(
			recurringTransactionId: recurringTransactionId,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<ActivateRecurringTransactionCommand>(predicate: command =>
				command!.UserId == CallerId && command.RecurringTransactionId == recurringTransactionId
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Deactivate_ShouldAttributeTheCommandToTheCaller()
	{
		Guid recurringTransactionId = Guid.CreateVersion7();

		ISender sender = SenderReturning<DeactivateRecurringTransactionCommand>(result: Ok());

		await DeactivateRecurringTransactionEndpoint.HandleAsync(
			recurringTransactionId: recurringTransactionId,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<DeactivateRecurringTransactionCommand>(predicate: command =>
				command!.UserId == CallerId && command.RecurringTransactionId == recurringTransactionId
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ChangeAmount_ShouldPassTheFigureThroughUnchanged()
	{
		Guid recurringTransactionId = Guid.CreateVersion7();

		ISender sender = SenderReturning<ChangeRecurringTransactionAmountCommand>(result: Ok());

		await ChangeRecurringTransactionAmountEndpoint.HandleAsync(
			recurringTransactionId: recurringTransactionId,
			request: new ChangeRecurringTransactionAmountRequest(Amount: 7_500m),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<ChangeRecurringTransactionAmountCommand>(predicate: command =>
				command!.UserId == CallerId &&
				command.RecurringTransactionId == recurringTransactionId &&
				command.Amount == 7_500m
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ChangeCurrency_WithAnInvalidCode_ShouldNotReachTheHandler()
	{
		ISender sender = Substitute.For<ISender>();

		await ChangeRecurringTransactionCurrencyEndpoint.HandleAsync(
			recurringTransactionId: Guid.CreateVersion7(),
			request: new ChangeRecurringTransactionCurrencyRequest(Currency: "RUBLE"),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<ChangeRecurringTransactionCurrencyCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task ChangeCurrency_WithAValidCode_ShouldSendOnlyTheCurrency()
	{
		Guid recurringTransactionId = Guid.CreateVersion7();

		ISender sender = SenderReturning<ChangeRecurringTransactionCurrencyCommand>(result: Ok());

		await ChangeRecurringTransactionCurrencyEndpoint.HandleAsync(
			recurringTransactionId: recurringTransactionId,
			request: new ChangeRecurringTransactionCurrencyRequest(Currency: "USD"),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<ChangeRecurringTransactionCurrencyCommand>(predicate: command =>
				command!.UserId == CallerId &&
				command.RecurringTransactionId == recurringTransactionId &&
				command.Currency.Value == "USD"
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	[Arguments(1)]
	[Arguments(15)]
	[Arguments(31)]
	[Arguments(0)]
	[Arguments(32)]
	public async Task ChangeDayOfMonth_ShouldPassTheDayThroughUnchanged(int dayOfMonth)
	{
		Guid recurringTransactionId = Guid.CreateVersion7();

		ISender sender = SenderReturning<ChangeRecurringTransactionDayOfMonthCommand>(result: Ok());

		await ChangeRecurringTransactionDayOfMonthEndpoint.HandleAsync(
			recurringTransactionId: recurringTransactionId,
			request: new ChangeRecurringTransactionDayOfMonthRequest(DayOfMonth: dayOfMonth),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<ChangeRecurringTransactionDayOfMonthCommand>(predicate: command =>
				command!.UserId == CallerId &&
				command.RecurringTransactionId == recurringTransactionId &&
				command.DayOfMonth == dayOfMonth
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Create_WithoutAnIdempotencyKey_ShouldNotReachTheHandler()
	{
		using MemoryStream body = new MemoryStream();

		ISender sender = Substitute.For<ISender>();

		await CreateRecurringTransactionEndpoint.HandleAsync(
			request: CreateRequest(),
			httpContext: Context(body: body),
			currentUser: CurrentUser(),
			linkGenerator: Substitute.For<LinkGenerator>(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<CreateRecurringTransactionCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Create_WithAnInvalidCurrency_ShouldNotReachTheHandler()
	{
		using MemoryStream body = new MemoryStream();

		ISender sender = Substitute.For<ISender>();

		await CreateRecurringTransactionEndpoint.HandleAsync(
			request: CreateRequest(currency: "RUBLE"),
			httpContext: Context(body: body, idempotencyKey: Guid.CreateVersion7()),
			currentUser: CurrentUser(),
			linkGenerator: Substitute.For<LinkGenerator>(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<CreateRecurringTransactionCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Create_WithAValidRequest_ShouldSendACommandBuiltFromIt()
	{
		using MemoryStream body = new MemoryStream();

		Guid accountId = Guid.CreateVersion7();
		Guid categoryId = Guid.CreateVersion7();
		Guid idempotencyKey = Guid.CreateVersion7();

		ISender sender = SenderReturning<CreateRecurringTransactionCommand>(result: Ok());

		await CreateRecurringTransactionEndpoint.HandleAsync(
			request: CreateRequest(
				accountId: accountId,
				categoryId: categoryId,
				amount: 3_200m,
				currency: "RUB",
				direction: DirectionType.Debit,
				dayOfMonth: 5,
				description: "Аренда"
			),
			httpContext: Context(body: body, idempotencyKey: idempotencyKey),
			currentUser: CurrentUser(),
			linkGenerator: Substitute.For<LinkGenerator>(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<CreateRecurringTransactionCommand>(predicate: command =>
				command!.UserId == CallerId &&
				command.AccountId == accountId &&
				command.CategoryId == categoryId &&
				command.Amount == 3_200m &&
				command.Currency.Value == "RUB" &&
				command.Direction == DirectionType.Debit &&
				command.DayOfMonth == 5 &&
				command.Description == "Аренда" &&
				command.IdempotencyKey == idempotencyKey
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetRecurringTransaction_ShouldQueryForTheCallersOwnRecord()
	{
		Guid recurringTransactionId = Guid.CreateVersion7();

		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<GetRecurringTransactionQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<RecurringTransactionReadModel, AppException>.Failure(
			error: new NotFoundException(message: "Recurring transaction not found.", id: recurringTransactionId)
		));

		await GetRecurringTransactionEndpoint.HandleAsync(
			recurringTransactionId: recurringTransactionId,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetRecurringTransactionQuery>(predicate: query =>
				query!.RecurringTransactionId == recurringTransactionId && query.UserId == CallerId
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetRecurringTransactions_WithoutACursor_ShouldAskForTheFirstPage()
	{
		ISender sender = SenderForListing();

		await GetRecurringTransactionsEndpoint.HandleAsync(
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetRecurringTransactionsQuery>(predicate: query =>
				query!.UserId == CallerId &&
				query.CursorCreatedAt == null &&
				query.CursorId == null
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetRecurringTransactions_ShouldNormaliseTheCursorToUtc()
	{
		DateTimeOffset cursor = new DateTimeOffset(year: 2026, month: 9, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.FromHours(value: 3));

		ISender sender = SenderForListing();

		await GetRecurringTransactionsEndpoint.HandleAsync(
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None,
			cursorCreatedAt: cursor
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetRecurringTransactionsQuery>(predicate: query => query!.CursorCreatedAt == cursor.ToUniversalTime()),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}
