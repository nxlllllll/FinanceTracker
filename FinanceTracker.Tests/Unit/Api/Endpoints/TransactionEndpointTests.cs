using FinanceTracker.Api.Endpoints.Transactions.Commands;
using FinanceTracker.Api.Endpoints.Transactions.Contracts;
using FinanceTracker.Api.Endpoints.Transactions.Queries;
using FinanceTracker.Api.Http;
using FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionCategory;
using FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionDescription;
using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transaction.Commands.ExcludeTransaction;
using FinanceTracker.Application.UseCases.Transaction.Commands.IncludeTransaction;
using FinanceTracker.Application.UseCases.Transaction.Queries.GetTransaction;
using FinanceTracker.Application.UseCases.Transaction.Queries.GetTransactions;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Tests.Unit.Api.Endpoints;

public sealed class TransactionEndpointTests
{
	private static readonly Guid CallerId = Guid.CreateVersion7();

	private static ICurrentUserProvider CurrentUser()
	{
		ICurrentUserProvider currentUser = Substitute.For<ICurrentUserProvider>();
		currentUser.UserId.Returns(returnThis: CallerId);

		return currentUser;
	}

	private static ISender SenderReturning<TCommand, TValue>(
		Result<TValue, AppException> result
	) where TCommand : IRequest<Result<TValue, AppException>>
	{
		ISender sender = Substitute.For<ISender>();

		sender.Send(
			request: Arg.Any<TCommand>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: result);

		return sender;
	}

	private static Result<Guid, AppException> Ok(Guid id) => Result<Guid, AppException>.Success(value: id);

	private static Result<Guid, AppException> NotFound(Guid id)
		=> Result<Guid, AppException>.Failure(error: new NotFoundException(message: "Transaction not found.", id: id));

	private static ISender SenderForListing()
	{
		ISender sender = Substitute.For<ISender>();

		sender.Send(
			request: Arg.Any<GetTransactionsQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<PagedResult<TransactionReadModel>, AppException>.Success(value: new PagedResult<TransactionReadModel>(
			Items: [],
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		)));

		return sender;
	}

	private static HttpContext ContextWithIdempotencyKey(Stream body, Guid key)
	{
		HttpContext context = HttpContextFactory.Create(body: body, method: "POST", path: "/api/v1/accounts");
		context.Request.Headers["Idempotency-Key"] = key.ToString();

		return context;
	}

	private static CreateTransactionRequest CreateRequest(
		string currency = "RUB",
		DateTimeOffset? occurredAt = null
	) => new CreateTransactionRequest(
		CategoryId: Guid.CreateVersion7(),
		Amount: 1_500m,
		Currency: currency,
		Direction: DirectionType.Debit,
		Description: null,
		OccurredAt: occurredAt ?? DateTimeOffset.UtcNow
	);

	[Test]
	public async Task ChangeCategory_ShouldAttributeTheCommandToTheCaller()
	{
		Guid transactionId = Guid.CreateVersion7();
		Guid categoryId = Guid.CreateVersion7();

		ISender sender = SenderReturning<ChangeTransactionCategoryCommand, Guid>(result: Ok(id: transactionId));

		await ChangeTransactionCategoryEndpoint.HandleAsync(
			transactionId: transactionId,
			request: new ChangeTransactionCategoryRequest(CategoryId: categoryId),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<ChangeTransactionCategoryCommand>(predicate: command =>
				command!.UserId == CallerId &&
				command.TransactionId == transactionId &&
				command.CategoryId == categoryId
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ChangeCategory_WhenTheHandlerFails_ShouldNotReturnNoContent()
	{
		Guid transactionId = Guid.CreateVersion7();

		ISender sender = SenderReturning<ChangeTransactionCategoryCommand, Guid>(result: NotFound(id: transactionId));

		IResult result = await ChangeTransactionCategoryEndpoint.HandleAsync(
			transactionId: transactionId,
			request: new ChangeTransactionCategoryRequest(CategoryId: Guid.CreateVersion7()),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result.GetType().Name).DoesNotContain(expected: "NoContent").Because(message: """
			A failure mapped to 204 tells the caller their change went through when it did not.
		""");
	}

	[Test]
	public async Task ChangeDescription_ShouldPassTheDescriptionThroughUnchanged()
	{
		Guid transactionId = Guid.CreateVersion7();

		ISender sender = SenderReturning<ChangeTransactionDescriptionCommand, Guid>(result: Ok(id: transactionId));

		await ChangeTransactionDescriptionEndpoint.HandleAsync(
			transactionId: transactionId,
			request: new ChangeTransactionDescriptionRequest(Description: "  spaced  "),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<ChangeTransactionDescriptionCommand>(predicate: command =>
				command!.UserId == CallerId &&
				command.Description == "  spaced  "
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ChangeDescription_WithNull_ShouldSendNull()
	{
		Guid transactionId = Guid.CreateVersion7();

		ISender sender = SenderReturning<ChangeTransactionDescriptionCommand, Guid>(result: Ok(id: transactionId));

		await ChangeTransactionDescriptionEndpoint.HandleAsync(
			transactionId: transactionId,
			request: new ChangeTransactionDescriptionRequest(Description: null),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<ChangeTransactionDescriptionCommand>(predicate: command => command!.Description == null),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Exclude_ShouldAttributeTheCommandToTheCaller()
	{
		Guid transactionId = Guid.CreateVersion7();

		ISender sender = SenderReturning<ExcludeTransactionCommand, Guid>(result: Ok(id: transactionId));

		await ExcludeTransactionEndpoint.HandleAsync(
			transactionId: transactionId,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<ExcludeTransactionCommand>(predicate: command =>
				command!.UserId == CallerId && command.TransactionId == transactionId
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Include_ShouldAttributeTheCommandToTheCaller()
	{
		Guid transactionId = Guid.CreateVersion7();

		ISender sender = SenderReturning<IncludeTransactionCommand, Guid>(result: Ok(id: transactionId));

		await IncludeTransactionEndpoint.HandleAsync(
			transactionId: transactionId,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<IncludeTransactionCommand>(predicate: command =>
				command!.UserId == CallerId && command.TransactionId == transactionId
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Create_WithoutAnIdempotencyKey_ShouldNotReachTheHandler()
	{
		using MemoryStream body = new MemoryStream();
		HttpContext context = HttpContextFactory.Create(body: body, method: "POST", path: "/api/v1/accounts");

		ISender sender = Substitute.For<ISender>();

		await CreateTransactionEndpoint.HandleAsync(
			accountId: Guid.CreateVersion7(),
			request: CreateRequest(),
			httpContext: context,
			currentUser: CurrentUser(),
			linkGenerator: Substitute.For<LinkGenerator>(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(
			request: Arg.Any<CreateTransactionCommand>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Create_WithAnInvalidCurrency_ShouldNotReachTheHandler()
	{
		using MemoryStream body = new MemoryStream();
		HttpContext context = ContextWithIdempotencyKey(body: body, key: Guid.CreateVersion7());

		ISender sender = Substitute.For<ISender>();

		await CreateTransactionEndpoint.HandleAsync(
			accountId: Guid.CreateVersion7(),
			request: CreateRequest(currency: "RUBLE"),
			httpContext: context,
			currentUser: CurrentUser(),
			linkGenerator: Substitute.For<LinkGenerator>(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<CreateTransactionCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Create_ShouldNormaliseTheOccurredAtToUtc()
	{
		using MemoryStream body = new MemoryStream();
		Guid idempotencyKey = Guid.CreateVersion7();
		HttpContext context = ContextWithIdempotencyKey(body: body, key: idempotencyKey);

		DateTimeOffset supplied = new DateTimeOffset(year: 2026, month: 9, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.FromHours(value: 12));

		ISender sender = SenderReturning<CreateTransactionCommand, Guid>(result: Ok(id: Guid.CreateVersion7()));

		await CreateTransactionEndpoint.HandleAsync(
			accountId: Guid.CreateVersion7(),
			request: CreateRequest(occurredAt: supplied),
			httpContext: context,
			currentUser: CurrentUser(),
			linkGenerator: Substitute.For<LinkGenerator>(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<CreateTransactionCommand>(predicate: command =>
				command!.UserId == CallerId &&
				command.IdempotencyKey == idempotencyKey &&
				command.OccurredAt == supplied.ToUniversalTime()
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetTransaction_ShouldQueryForTheCallersOwnRecord()
	{
		Guid transactionId = Guid.CreateVersion7();

		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<GetTransactionQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<TransactionReadModel, AppException>.Failure(
			error: new NotFoundException(message: "Transaction not found.", id: transactionId)
		));

		await GetTransactionEndpoint.HandleAsync(
			transactionId: transactionId,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetTransactionQuery>(predicate: query =>
				query!.TransactionId == transactionId && query.UserId == CallerId
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetTransactions_WithAnUnparsableDirection_ShouldNotReachTheHandler()
	{
		ISender sender = Substitute.For<ISender>();

		await GetTransactionsEndpoint.HandleAsync(
			accountId: Guid.CreateVersion7(),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None,
			direction: "sideways"
		);

		await sender.DidNotReceive().Send(
			request: Arg.Any<GetTransactionsQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetTransactions_WithADirectionInAnyCasing_ShouldParseIt()
	{
		ISender sender = SenderForListing();

		await GetTransactionsEndpoint.HandleAsync(
			accountId: Guid.CreateVersion7(),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None,
			direction: "dEbIt"
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetTransactionsQuery>(predicate: query => query!.Direction == DirectionType.Debit),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetTransactions_WithoutADirection_ShouldNotFilterByIt()
	{
		ISender sender = SenderForListing();

		await GetTransactionsEndpoint.HandleAsync(
			accountId: Guid.CreateVersion7(),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetTransactionsQuery>(predicate: query => query!.Direction == null),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetTransactions_ShouldNormaliseEveryDateToUtc()
	{
		DateTimeOffset from = new DateTimeOffset(year: 2026, month: 9, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.FromHours(value: 12));
		DateTimeOffset to = new DateTimeOffset(year: 2026, month: 9, day: 30, hour: 0, minute: 0, second: 0, offset: TimeSpan.FromHours(value: -10));
		DateTimeOffset cursor = new DateTimeOffset(year: 2026, month: 9, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.FromHours(value: 3));

		ISender sender = SenderForListing();

		await GetTransactionsEndpoint.HandleAsync(
			accountId: Guid.CreateVersion7(),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None,
			dateFrom: from,
			dateTo: to,
			cursorOccurredAt: cursor
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetTransactionsQuery>(predicate: query =>
				query!.DateFrom == from.ToUniversalTime() &&
				query.DateTo == to.ToUniversalTime() &&
				query.CursorOccurredAt == cursor.ToUniversalTime()
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}
