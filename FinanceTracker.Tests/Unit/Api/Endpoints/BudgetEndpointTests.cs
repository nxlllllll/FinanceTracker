using FinanceTracker.Api.Endpoints.Budgets.Commands;
using FinanceTracker.Api.Endpoints.Budgets.Contracts;
using FinanceTracker.Api.Endpoints.Budgets.Queries;
using FinanceTracker.Api.Http;
using FinanceTracker.Application.UseCases.Budget.Commands.ActivateBudget;
using FinanceTracker.Application.UseCases.Budget.Commands.ChangeBudgetAmount;
using FinanceTracker.Application.UseCases.Budget.Commands.ChangeBudgetPeriod;
using FinanceTracker.Application.UseCases.Budget.Commands.CreateBudget;
using FinanceTracker.Application.UseCases.Budget.Commands.DeactivateBudget;
using FinanceTracker.Application.UseCases.Budget.Queries.GetBudget;
using FinanceTracker.Application.UseCases.Budget.Queries.GetBudgetProgress;
using FinanceTracker.Application.UseCases.Budget.Queries.GetBudgets;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Api.Endpoints;

public sealed class BudgetEndpointTests
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
		HttpContext context = HttpContextFactory.Create(body: body, method: "POST", path: "/api/v1/budgets");

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
			request: Arg.Any<GetBudgetsQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<PagedResult<BudgetReadModel>, AppException>.Success(value: new PagedResult<BudgetReadModel>(
			Items: [],
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		)));

		return sender;
	}

	private static CreateBudgetRequest CreateRequest(
		Guid? categoryId = null,
		decimal amount = 10_000m,
		string currency = "RUB",
		DateOnly? from = null,
		DateOnly? to = null
	) => new CreateBudgetRequest(
		CategoryId: categoryId ?? Guid.CreateVersion7(),
		Amount: amount,
		Currency: currency,
		From: from ?? new DateOnly(year: 2026, month: 9, day: 1),
		To: to ?? new DateOnly(year: 2026, month: 9, day: 30)
	);

	[Test]
	public async Task Activate_ShouldAttributeTheCommandToTheCaller()
	{
		Guid budgetId = Guid.CreateVersion7();

		ISender sender = SenderReturning<ActivateBudgetCommand>(result: Ok());

		await ActivateBudgetEndpoint.HandleAsync(
			budgetId: budgetId,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<ActivateBudgetCommand>(predicate: command =>
				command!.UserId == CallerId && command.BudgetId == budgetId
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Deactivate_ShouldAttributeTheCommandToTheCaller()
	{
		Guid budgetId = Guid.CreateVersion7();

		ISender sender = SenderReturning<DeactivateBudgetCommand>(result: Ok());

		await DeactivateBudgetEndpoint.HandleAsync(
			budgetId: budgetId,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<DeactivateBudgetCommand>(predicate: command =>
				command!.UserId == CallerId && command.BudgetId == budgetId
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	[Arguments(0)]
	[Arguments(-100)]
	[Arguments(15_000)]
	public async Task ChangeAmount_ShouldPassTheFigureThroughUnchanged(decimal amount)
	{
		Guid budgetId = Guid.CreateVersion7();

		ISender sender = SenderReturning<ChangeBudgetAmountCommand>(result: Ok());

		await ChangeBudgetAmountEndpoint.HandleAsync(
			budgetId: budgetId,
			request: new ChangeBudgetAmountRequest(Amount: amount),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<ChangeBudgetAmountCommand>(predicate: command =>
				command!.UserId == CallerId &&
				command.BudgetId == budgetId &&
				command.Amount == amount
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ChangePeriod_ShouldCarryBothBoundsUnchanged()
	{
		Guid budgetId = Guid.CreateVersion7();

		DateOnly from = new DateOnly(year: 2026, month: 9, day: 1);
		DateOnly to = new DateOnly(year: 2026, month: 9, day: 30);

		ISender sender = SenderReturning<ChangeBudgetPeriodCommand>(result: Ok());

		await ChangeBudgetPeriodEndpoint.HandleAsync(
			budgetId: budgetId,
			request: new ChangeBudgetPeriodRequest(From: from, To: to),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<ChangeBudgetPeriodCommand>(predicate: command =>
				command!.UserId == CallerId &&
				command.BudgetId == budgetId &&
				command.From == from &&
				command.To == to
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ChangePeriod_WithAnInvertedSpan_ShouldStillReachTheHandler()
	{
		ISender sender = SenderReturning<ChangeBudgetPeriodCommand>(result: Ok());

		await ChangeBudgetPeriodEndpoint.HandleAsync(
			budgetId: Guid.CreateVersion7(),
			request: new ChangeBudgetPeriodRequest(
				From: new DateOnly(year: 2026, month: 9, day: 30),
				To: new DateOnly(year: 2026, month: 9, day: 1)
			),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Any<ChangeBudgetPeriodCommand>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Create_WithoutAnIdempotencyKey_ShouldNotReachTheHandler()
	{
		using MemoryStream body = new MemoryStream();

		ISender sender = Substitute.For<ISender>();

		await CreateBudgetEndpoint.HandleAsync(
			request: CreateRequest(),
			httpContext: Context(body: body),
			currentUser: CurrentUser(),
			linkGenerator: Substitute.For<LinkGenerator>(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<CreateBudgetCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Create_WithAnInvalidCurrency_ShouldNotReachTheHandler()
	{
		using MemoryStream body = new MemoryStream();

		ISender sender = Substitute.For<ISender>();

		await CreateBudgetEndpoint.HandleAsync(
			request: CreateRequest(currency: "RUBLE"),
			httpContext: Context(body: body, idempotencyKey: Guid.CreateVersion7()),
			currentUser: CurrentUser(),
			linkGenerator: Substitute.For<LinkGenerator>(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<CreateBudgetCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Create_WithAValidRequest_ShouldSendACommandBuiltFromIt()
	{
		using MemoryStream body = new MemoryStream();

		Guid categoryId = Guid.CreateVersion7();
		Guid idempotencyKey = Guid.CreateVersion7();

		DateOnly from = new DateOnly(year: 2026, month: 9, day: 1);
		DateOnly to = new DateOnly(year: 2026, month: 9, day: 30);

		ISender sender = SenderReturning<CreateBudgetCommand>(result: Ok());

		await CreateBudgetEndpoint.HandleAsync(
			request: CreateRequest(categoryId: categoryId, amount: 30_000m, currency: "RUB", from: from, to: to),
			httpContext: Context(body: body, idempotencyKey: idempotencyKey),
			currentUser: CurrentUser(),
			linkGenerator: Substitute.For<LinkGenerator>(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<CreateBudgetCommand>(predicate: command =>
				command!.UserId == CallerId &&
				command.CategoryId == categoryId &&
				command.Currency.Value == "RUB" &&
				command.Amount == 30_000m &&
				command.From == from &&
				command.To == to &&
				command.IdempotencyKey == idempotencyKey
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetBudget_ShouldQueryForTheCallersOwnRecord()
	{
		Guid budgetId = Guid.CreateVersion7();

		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<GetBudgetQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<BudgetReadModel, AppException>.Failure(
			error: new NotFoundException(message: "Budget not found.", id: budgetId)
		));

		await GetBudgetEndpoint.HandleAsync(
			budgetId: budgetId,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetBudgetQuery>(predicate: query =>
				query!.BudgetId == budgetId && query.UserId == CallerId
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetBudgetProgress_ShouldQueryForTheCallersOwnRecord()
	{
		Guid budgetId = Guid.CreateVersion7();

		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<GetBudgetProgressQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<BudgetProgress, AppException>.Failure(
			error: new NotFoundException(message: "Budget not found.", id: budgetId)
		));

		await GetBudgetProgressEndpoint.HandleAsync(
			budgetId: budgetId,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetBudgetProgressQuery>(predicate: query =>
				query!.BudgetId == budgetId && query.UserId == CallerId
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetBudgets_WithoutACursor_ShouldAskForTheFirstPage()
	{
		ISender sender = SenderForListing();

		await GetBudgetsEndpoint.HandleAsync(
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetBudgetsQuery>(predicate: query =>
				query!.UserId == CallerId &&
				query.CursorCreatedAt == null &&
				query.CursorId == null
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetBudgets_ShouldNormaliseTheCursorToUtc()
	{
		DateTimeOffset cursor = new DateTimeOffset(year: 2026, month: 9, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.FromHours(value: 3));

		ISender sender = SenderForListing();

		await GetBudgetsEndpoint.HandleAsync(
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None,
			cursorCreatedAt: cursor
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetBudgetsQuery>(predicate: query => query!.CursorCreatedAt == cursor.ToUniversalTime()),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}
