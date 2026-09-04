using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Api.Endpoints.Transfers.Commands;
using FinanceTracker.Api.Endpoints.Transfers.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Application.UseCases.Transfer.Commands.CreateTransfer;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Tests.Unit.Api.Endpoints;

public sealed class CreateTransferEndpointTests
{
	private static readonly Guid CallerId = Guid.CreateVersion7();

	private static ICurrentUserProvider CurrentUser()
	{
		ICurrentUserProvider currentUser = Substitute.For<ICurrentUserProvider>();
		currentUser.UserId.Returns(returnThis: CallerId);

		return currentUser;
	}

	private static HttpContext Context(Stream body, Guid? idempotencyKey)
	{
		HttpContext context = HttpContextFactory.Create(body: body, method: "POST", path: "/api/v1/accounts");

		if (idempotencyKey is not null)
			context.Request.Headers["Idempotency-Key"] = idempotencyKey.Value.ToString();

		return context;
	}

	private static ISender SenderReturning(Result<Guid, AppException> result)
	{
		ISender sender = Substitute.For<ISender>();

		sender.Send(
			request: Arg.Any<CreateTransferCommand>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: result);

		return sender;
	}

	private static CreateTransferRequest Request(
		Guid? toAccountId = null,
		decimal amount = 1_000m
	) => new CreateTransferRequest(
		ToAccountId: toAccountId ?? Guid.CreateVersion7(),
		Amount: amount,
		Description: null
	);

	private static DefaultHttpContext ResponseContext(Stream body) => new DefaultHttpContext
	{
		RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
		Response = { Body = body }
	};

	[Test]
	public async Task HandleAsync_WithoutAnIdempotencyKey_ShouldNotReachTheHandler()
	{
		using MemoryStream body = new MemoryStream();

		ISender sender = Substitute.For<ISender>();

		await CreateTransferEndpoint.HandleAsync(
			accountId: Guid.CreateVersion7(),
			request: Request(),
			httpContext: Context(body: body, idempotencyKey: null),
			currentUser: CurrentUser(),
			sender: sender,
			linkGenerator: Substitute.For<LinkGenerator>(),
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<CreateTransferCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task HandleAsync_ShouldTakeTheSourceFromTheRouteAndTheDestinationFromTheBody()
	{
		using MemoryStream body = new MemoryStream();

		Guid fromAccountId = Guid.CreateVersion7();
		Guid toAccountId = Guid.CreateVersion7();
		Guid idempotencyKey = Guid.CreateVersion7();

		ISender sender = SenderReturning(result: Result<Guid, AppException>.Success(value: Guid.CreateVersion7()));

		await CreateTransferEndpoint.HandleAsync(
			accountId: fromAccountId,
			request: Request(toAccountId: toAccountId, amount: 2_500m),
			httpContext: Context(body: body, idempotencyKey: idempotencyKey),
			currentUser: CurrentUser(),
			sender: sender,
			linkGenerator: Substitute.For<LinkGenerator>(),
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<CreateTransferCommand>(predicate: command =>
				command!.UserId == CallerId &&
				command.FromAccountId == fromAccountId &&
				command.ToAccountId == toAccountId &&
				command.Amount == 2_500m &&
				command.IdempotencyKey == idempotencyKey
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_OnSuccess_ShouldAnswerAcceptedRatherThanCreated()
	{
		using MemoryStream body = new MemoryStream();

		Guid transferId = Guid.CreateVersion7();

		ISender sender = SenderReturning(result: Result<Guid, AppException>.Success(value: transferId));

		IResult result = await CreateTransferEndpoint.HandleAsync(
			accountId: Guid.CreateVersion7(),
			request: Request(),
			httpContext: Context(body: body, idempotencyKey: Guid.CreateVersion7()),
			currentUser: CurrentUser(),
			sender: sender,
			linkGenerator: Substitute.For<LinkGenerator>(),
			ct: CancellationToken.None
		);

		using MemoryStream responseBody = new MemoryStream();
		DefaultHttpContext responseContext = ResponseContext(body: responseBody);

		await result.ExecuteAsync(httpContext: responseContext);

		await Assert.That(value: responseContext.Response.StatusCode).IsEqualTo(expected: StatusCodes.Status202Accepted).Because(message: """
			The debit is committed but the credit is applied by a worker and can still be compensated.
			201 would promise a completed transfer; 202 says the request was taken and the outcome is
			still being decided.
		""");
	}

	[Test]
	public async Task HandleAsync_OnFailure_ShouldNotAnswerAccepted()
	{
		using MemoryStream body = new MemoryStream();

		ISender sender = SenderReturning(result: Result<Guid, AppException>.Failure(
			error: new NotFoundException(message: "Account not found.", id: Guid.CreateVersion7())
		));

		IResult result = await CreateTransferEndpoint.HandleAsync(
			accountId: Guid.CreateVersion7(),
			request: Request(),
			httpContext: Context(body: body, idempotencyKey: Guid.CreateVersion7()),
			currentUser: CurrentUser(),
			sender: sender,
			linkGenerator: Substitute.For<LinkGenerator>(),
			ct: CancellationToken.None
		);

		using MemoryStream responseBody = new MemoryStream();
		DefaultHttpContext responseContext = ResponseContext(body: responseBody);

		await result.ExecuteAsync(httpContext: responseContext);

		await Assert.That(value: responseContext.Response.StatusCode).IsNotEqualTo(notExpected: StatusCodes.Status202Accepted).Because(message: """
			A rejected transfer answering 202 would tell the caller their money is on its way when
			nothing was debited at all.
		""");
	}
}
