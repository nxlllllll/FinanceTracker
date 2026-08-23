using FinanceTracker.Api.Endpoints.Auth.Commands;
using FinanceTracker.Api.Endpoints.Auth.Contracts;
using FinanceTracker.Application.UseCases.User.Commands.RegisterUser;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Tests.Unit.Api.Endpoints;

public sealed class RegisterEndpointTests
{
	private const string ValidEmail = "user@test.com";
	private const string ValidCurrency = "RUB";
	private const string ValidPassword = "P@ssw0rd!";

	private static (HttpContext Context, MemoryStream Body) CreateContext(Guid? idempotencyKey)
	{
		MemoryStream body = new MemoryStream();
		HttpContext context = HttpContextFactory.Create(body: body, method: "POST", path: "/api/v1/auth/register");

		if (idempotencyKey is not null)
			context.Request.Headers["Idempotency-Key"] = idempotencyKey.Value.ToString();

		return (context, body);
	}

	private static ISender SenderReturning(Guid userId)
	{
		ISender sender = Substitute.For<ISender>();

		sender.Send(request: Arg.Any<RegisterUserCommand>(), cancellationToken: Arg.Any<CancellationToken>())
			.Returns(returnThis: Result<Guid, AppException>.Success(value: userId));

		return sender;
	}

	[Test]
	public async Task HandleAsync_WithoutAnIdempotencyKey_ShouldNotReachTheHandler()
	{
		(HttpContext context, MemoryStream body) = CreateContext(idempotencyKey: null);
		using MemoryStream _ = body;

		ISender sender = Substitute.For<ISender>();

		IResult result = await RegisterEndpoint.HandleAsync(
			request: new RegisterUserRequest(Email: ValidEmail, Password: ValidPassword, BaseCurrency: ValidCurrency),
			sender: sender,
			httpContext: context,
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNotNull();

		await sender.DidNotReceive().Send(request: Arg.Any<RegisterUserCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task HandleAsync_WithAnInvalidEmail_ShouldNotReachTheHandler()
	{
		(HttpContext context, MemoryStream body) = CreateContext(idempotencyKey: Guid.CreateVersion7());
		using MemoryStream _ = body;

		ISender sender = Substitute.For<ISender>();

		await RegisterEndpoint.HandleAsync(
			request: new RegisterUserRequest(Email: "not-an-email", Password: ValidPassword, BaseCurrency: ValidCurrency),
			sender: sender,
			httpContext: context,
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<RegisterUserCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task HandleAsync_WithAnInvalidCurrency_ShouldNotReachTheHandler()
	{
		(HttpContext context, MemoryStream body) = CreateContext(idempotencyKey: Guid.CreateVersion7());
		using MemoryStream _ = body;

		ISender sender = Substitute.For<ISender>();

		await RegisterEndpoint.HandleAsync(
			request: new RegisterUserRequest(Email: ValidEmail, Password: ValidPassword, BaseCurrency: "RUBLE"),
			sender: sender,
			httpContext: context,
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<RegisterUserCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task HandleAsync_WithAValidRequest_ShouldSendACommandBuiltFromIt()
	{
		Guid idempotencyKey = Guid.CreateVersion7();

		(HttpContext context, MemoryStream body) = CreateContext(idempotencyKey: idempotencyKey);
		using MemoryStream _ = body;

		ISender sender = SenderReturning(userId: Guid.CreateVersion7());

		await RegisterEndpoint.HandleAsync(
			request: new RegisterUserRequest(Email: ValidEmail, Password: ValidPassword, BaseCurrency: ValidCurrency),
			sender: sender,
			httpContext: context,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<RegisterUserCommand>(predicate: command =>
				command!.Email.Value == ValidEmail &&
				command.BaseCurrencyCode.Value == ValidCurrency &&
				command.Password == ValidPassword &&
				command.IdempotencyKey == idempotencyKey
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithoutATimeZone_ShouldSendNullRatherThanADefault()
	{
		(HttpContext context, MemoryStream body) = CreateContext(idempotencyKey: Guid.CreateVersion7());
		using MemoryStream _ = body;

		ISender sender = SenderReturning(userId: Guid.CreateVersion7());

		await RegisterEndpoint.HandleAsync(
			request: new RegisterUserRequest(Email: ValidEmail, Password: ValidPassword, BaseCurrency: ValidCurrency),
			sender: sender,
			httpContext: context,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<RegisterUserCommand>(predicate: command => command!.TimeZone == null),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithAValidTimeZone_ShouldPassItThrough()
	{
		(HttpContext context, MemoryStream body) = CreateContext(idempotencyKey: Guid.CreateVersion7());
		using MemoryStream _ = body;

		ISender sender = SenderReturning(userId: Guid.CreateVersion7());

		await RegisterEndpoint.HandleAsync(
			request: new RegisterUserRequest(Email: ValidEmail, Password: ValidPassword, BaseCurrency: ValidCurrency, TimeZone: "Pacific/Auckland"),
			sender: sender,
			httpContext: context,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<RegisterUserCommand>(predicate: command => command!.TimeZone!.Value.Value == "Pacific/Auckland"),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithAnInvalidTimeZone_ShouldNotReachTheHandler()
	{
		(HttpContext context, MemoryStream body) = CreateContext(idempotencyKey: Guid.CreateVersion7());
		using MemoryStream _ = body;

		ISender sender = Substitute.For<ISender>();

		await RegisterEndpoint.HandleAsync(
			request: new RegisterUserRequest(Email: ValidEmail, Password: ValidPassword, BaseCurrency: ValidCurrency, TimeZone: "Mars/Olympus"),
			sender: sender,
			httpContext: context,
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<RegisterUserCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}
}
