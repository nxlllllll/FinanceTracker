using FinanceTracker.Api.Endpoints.Categories.Commands;
using FinanceTracker.Api.Endpoints.Categories.Contracts;
using FinanceTracker.Api.Endpoints.Categories.Queries;
using FinanceTracker.Api.Http;
using FinanceTracker.Application.UseCases.Category.Commands.ArchiveCategory;
using FinanceTracker.Application.UseCases.Category.Commands.CreateCategory;
using FinanceTracker.Application.UseCases.Category.Commands.RenameCategory;
using FinanceTracker.Application.UseCases.Category.Commands.UnarchiveCategory;
using FinanceTracker.Application.UseCases.Category.Queries.GetCategories;
using FinanceTracker.Application.UseCases.Category.Queries.GetCategory;
using FinanceTracker.Application.UseCases.Category.Queries.GetTotal;
using FinanceTracker.Application.UseCases.Category.Queries.GetTotalsByPeriod;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Api.Endpoints;

public sealed class CategoryEndpointTests
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
		HttpContext context = HttpContextFactory.Create(body: body, method: "POST", path: "/api/v1/categories");

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
			request: Arg.Any<GetCategoriesQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<PagedResult<CategoryReadModel>, AppException>.Success(value: new PagedResult<CategoryReadModel>(
			Items: [],
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		)));

		return sender;
	}

	private static CreateCategoryRequest CreateRequest(
		string name = "Продукты",
		CategoryType type = CategoryType.Expense,
		Guid? parentId = null
	) => new CreateCategoryRequest(
		Name: name,
		Type: type,
		ParentId: parentId
	);

	[Test]
	public async Task Archive_ShouldAttributeTheCommandToTheCaller()
	{
		Guid categoryId = Guid.CreateVersion7();

		ISender sender = SenderReturning<ArchiveCategoryCommand>(result: Ok());

		await ArchiveCategoryEndpoint.HandleAsync(
			categoryId: categoryId,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<ArchiveCategoryCommand>(predicate: command =>
				command!.UserId == CallerId && command.CategoryId == categoryId
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Unarchive_ShouldAttributeTheCommandToTheCaller()
	{
		Guid categoryId = Guid.CreateVersion7();

		ISender sender = SenderReturning<UnarchiveCategoryCommand>(result: Ok());

		await UnarchiveCategoryEndpoint.HandleAsync(
			categoryId: categoryId,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<UnarchiveCategoryCommand>(predicate: command =>
				command!.UserId == CallerId && command.CategoryId == categoryId
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Rename_WithAnInvalidName_ShouldNotReachTheHandler()
	{
		ISender sender = Substitute.For<ISender>();

		await RenameCategoryEndpoint.HandleAsync(
			categoryId: Guid.CreateVersion7(),
			request: new RenameCategoryRequest(Name: "   "),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<RenameCategoryCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Rename_WithAValidName_ShouldSendIt()
	{
		Guid categoryId = Guid.CreateVersion7();

		ISender sender = SenderReturning<RenameCategoryCommand>(result: Ok());

		await RenameCategoryEndpoint.HandleAsync(
			categoryId: categoryId,
			request: new RenameCategoryRequest(Name: "Продукты"),
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<RenameCategoryCommand>(predicate: command =>
				command!.UserId == CallerId &&
				command.CategoryId == categoryId &&
				command.NewName.Value == "Продукты"
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Create_WithoutAnIdempotencyKey_ShouldNotReachTheHandler()
	{
		using MemoryStream body = new MemoryStream();

		ISender sender = Substitute.For<ISender>();

		await CreateCategoryEndpoint.HandleAsync(
			request: CreateRequest(),
			httpContext: Context(body: body),
			currentUser: CurrentUser(),
			linkGenerator: Substitute.For<LinkGenerator>(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<CreateCategoryCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Create_WithAnInvalidName_ShouldNotReachTheHandler()
	{
		using MemoryStream body = new MemoryStream();

		ISender sender = Substitute.For<ISender>();

		await CreateCategoryEndpoint.HandleAsync(
			request: CreateRequest(name: "   "),
			httpContext: Context(body: body, idempotencyKey: Guid.CreateVersion7()),
			currentUser: CurrentUser(),
			linkGenerator: Substitute.For<LinkGenerator>(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<CreateCategoryCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Create_WithoutAParent_ShouldSendNull()
	{
		using MemoryStream body = new MemoryStream();

		ISender sender = SenderReturning<CreateCategoryCommand>(result: Ok());

		await CreateCategoryEndpoint.HandleAsync(
			request: CreateRequest(parentId: null),
			httpContext: Context(body: body, idempotencyKey: Guid.CreateVersion7()),
			currentUser: CurrentUser(),
			linkGenerator: Substitute.For<LinkGenerator>(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<CreateCategoryCommand>(predicate: command => command!.ParentId == null),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Create_WithAParent_ShouldSendACommandBuiltFromTheRequest()
	{
		using MemoryStream body = new MemoryStream();

		Guid parentId = Guid.CreateVersion7();
		Guid idempotencyKey = Guid.CreateVersion7();

		ISender sender = SenderReturning<CreateCategoryCommand>(result: Ok());

		await CreateCategoryEndpoint.HandleAsync(
			request: CreateRequest(name: "Кофе", type: CategoryType.Expense, parentId: parentId),
			httpContext: Context(body: body, idempotencyKey: idempotencyKey),
			currentUser: CurrentUser(),
			linkGenerator: Substitute.For<LinkGenerator>(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<CreateCategoryCommand>(predicate: command =>
				command!.UserId == CallerId &&
				command.Name.Value == "Кофе" &&
				command.Type == CategoryType.Expense &&
				command.ParentId == parentId &&
				command.IdempotencyKey == idempotencyKey
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetCategory_ShouldQueryForTheCallersOwnRecord()
	{
		Guid categoryId = Guid.CreateVersion7();

		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<GetCategoryQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<CategoryReadModel, AppException>.Failure(
			error: new NotFoundException(message: "Category not found.", id: categoryId)
		));

		await GetCategoryEndpoint.HandleAsync(
			categoryId: categoryId,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetCategoryQuery>(predicate: query =>
				query!.CategoryId == categoryId && query.UserId == CallerId
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetCategories_WithAnUnparsableType_ShouldNotReachTheHandler()
	{
		ISender sender = Substitute.For<ISender>();

		await GetCategoriesEndpoint.HandleAsync(
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None,
			type: "neither"
		);

		await sender.DidNotReceive().Send(request: Arg.Any<GetCategoriesQuery>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task GetCategories_WithATypeInAnyCasing_ShouldParseIt()
	{
		ISender sender = SenderForListing();

		await GetCategoriesEndpoint.HandleAsync(
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None,
			type: "eXpEnSe"
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetCategoriesQuery>(predicate: query => query!.Type == CategoryType.Expense),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetCategories_WithoutFilters_ShouldConstrainNothingButTheOwner()
	{
		ISender sender = SenderForListing();

		await GetCategoriesEndpoint.HandleAsync(
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetCategoriesQuery>(predicate: query =>
				query!.UserId == CallerId &&
				query.Type == null &&
				query.IsArchived == null &&
				query.ParentId == null
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetCategories_ShouldNormaliseTheCursorToUtc()
	{
		DateTimeOffset cursor = new DateTimeOffset(year: 2026, month: 9, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.FromHours(value: 3));

		ISender sender = SenderForListing();

		await GetCategoriesEndpoint.HandleAsync(
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None,
			cursorCreatedAt: cursor
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetCategoriesQuery>(predicate: query => query!.CursorCreatedAt == cursor.ToUniversalTime()),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetCategoryTotal_ShouldPassThePeriodThroughUnchanged()
	{
		Guid categoryId = Guid.CreateVersion7();
		DateOnly period = new DateOnly(year: 2026, month: 9, day: 17);

		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<GetTotalQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<CategoryTotalView, AppException>.Failure(
			error: new NotFoundException(message: "Category not found.", id: categoryId)
		));

		await GetCategoryTotalEndpoint.HandleAsync(
			categoryId: categoryId,
			period: period,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetTotalQuery>(predicate: query =>
				query!.UserId == CallerId &&
				query.CategoryId == categoryId &&
				query.Period == period
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetCategoryTotals_ShouldQueryForTheCallersOwnTotals()
	{
		DateOnly period = new DateOnly(year: 2026, month: 9, day: 1);

		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<GetTotalsByPeriodQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<CategoryTotalsView, AppException>.Success(
			value: new CategoryTotalsView(Totals: [], RecalculationPending: false)
		));

		await GetCategoryTotalsEndpoint.HandleAsync(
			period: period,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetTotalsByPeriodQuery>(predicate: query =>
				query!.UserId == CallerId && query.Period == period
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}
