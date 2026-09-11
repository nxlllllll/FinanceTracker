using FinanceTracker.Api.Http.Results;
using FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;
using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transfer.Commands.CreateTransfer;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Idempotency;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.RateLimit;
using FinanceTracker.Core.Exceptions.TransientExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context.Idempotency;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Integration.Api;

public sealed class FailureSurfaceTests : MediatorFixture
{
	private UserBuilder _userBuilder = null!;
	private CategoryBuilder _categoryBuilder = null!;
	private CurrencyBuilder _currencyBuilder = null!;

	[Before(hookType: Test)]
	public async Task SetupDataAsync()
	{
		_userBuilder = new UserBuilder(context: Context);
		_categoryBuilder = new CategoryBuilder(context: Context);
		_currencyBuilder = new CurrencyBuilder(context: Context);

		await _currencyBuilder.CreateAsync(code: "RUB");
		await _currencyBuilder.CreateAsync(code: "USD");
	}

	internal static DefaultHttpContext BuildContext()
	{
		ServiceCollection collection = new ServiceCollection();
		collection.AddLogging();
		collection.AddProblemDetails();

		return new DefaultHttpContext
		{
			RequestServices = collection.BuildServiceProvider(),
			Response = { Body = new MemoryStream() }
		};
	}

	internal static async Task<DefaultHttpContext> AnswerForThrownAsync(Func<Task> send)
	{
		DefaultHttpContext context = BuildContext();

		try
		{
			await send();
		}
		catch (Exception exception)
		{
			await new GlobalExceptionHandler(logger: NullLogger<GlobalExceptionHandler>.Instance)
				.TryHandleAsync(httpContext: context, exception: exception, cancellationToken: CancellationToken.None);
		}

		return context;
	}

	internal static async Task<DefaultHttpContext> AnswerForAsync(AppException error)
	{
		DefaultHttpContext context = BuildContext();
		await error.ToProblem().ExecuteAsync(httpContext: context);

		return context;
	}

	internal async Task<Guid> CreateAccountAsync(Guid userId, string currencyCode, decimal balance)
	{
		Result<Guid, AppException> result = await Mediator.Send(request: new CreateAccountCommand(
			UserId: userId,
			Name: Name.Create(value: "Основной счёт").Value,
			Type: AccountType.Checking,
			Currency: Currency.Create(value: currencyCode).Value,
			InitialBalance: balance
		)
		{ IdempotencyKey = Guid.CreateVersion7() });

		return result.Value!;
	}

	[Test]
	public async Task ATransferWithNoPublishedRate_ShouldAnswer503RatherThanAServerFault()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid fromAccountId = await CreateAccountAsync(userId: userId, currencyCode: "RUB", balance: 10_000m);
		Guid toAccountId = await CreateAccountAsync(userId: userId, currencyCode: "USD", balance: 0m);

		DefaultHttpContext context = await AnswerForThrownAsync(send: async () => await Mediator.Send(request: new CreateTransferCommand(
			UserId: userId,
			FromAccountId: fromAccountId,
			ToAccountId: toAccountId,
			Amount: 1_000m,
			Description: null
		)
		{ IdempotencyKey = Guid.CreateVersion7() }));

		await Assert.That(value: context.Response.StatusCode).IsEqualTo(expected: StatusCodes.Status503ServiceUnavailable).Because(message: """
			The rate job fills this gap on its own, so the caller should be told to come back. The
			conversion service signals by throwing rather than by returning a Result, so nothing in the
			pipeline turns it into a failure — the mapping is only reached if the exception handler
			applies it. This answered 500 until August, while the architecture test covering the mapping
			stayed green the whole time, because it checks the type hierarchy and not the path.
		""");

		await Assert.That(value: context.Response.Headers.RetryAfter.ToString()).IsEqualTo(expected: "60");
	}

	[Test]
	public async Task ACommandOnAnAbandonedIdempotencyKey_ShouldAnswer409()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid idempotencyKey = Guid.CreateVersion7();

		Context.IdempotentCommands.Add(entity: new IdempotentCommandEntity
		{
			IdempotencyKey = idempotencyKey,
			CommandType = nameof(CreateAccountCommand),
			UserId = userId,
			ReservationId = Guid.CreateVersion7(),
			ResponseJson = null,
			ReservedAt = DateTimeOffset.UtcNow.AddMinutes(minutes: -1),
			ExpiresAt = DateTimeOffset.UtcNow.AddHours(hours: 1)
		});
		await Context.SaveChangesAsync();

		Result<Guid, AppException> result = await Mediator.Send(request: new CreateAccountCommand(
			UserId: userId,
			Name: Name.Create(value: "Основной счёт").Value,
			Type: AccountType.Checking,
			Currency: Currency.Create(value: "RUB").Value,
			InitialBalance: 0m
		)
		{ IdempotencyKey = idempotencyKey });

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<IdempotencyAbandonedException>();

		DefaultHttpContext context = await AnswerForAsync(error: result.Error!);

		await Assert.That(value: context.Response.StatusCode).IsEqualTo(expected: StatusCodes.Status409Conflict).Because(message: """
			A reservation whose owner never came back is reclaimed, and the caller is asked to retry with
			the same key. Answering anything in the 5xx range would describe a broken server for a state
			the caller resolves by repeating the request.
		""");
	}

	[Test]
	public async Task ConcurrentWritesToOneAccount_ShouldNeverAnswerWithAServerFault()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountAsync(userId: userId, currencyCode: "RUB", balance: 0m);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId, name: "Зарплата", type: Core.Domains.Category.CategoryType.Income);

		const int writers = 8;

		IEnumerable<Task<AppException?>> attempts = Enumerable.Range(start: 0, count: writers).Select(selector: async _ =>
		{
			await using AsyncServiceScope scope = Host.Services.CreateAsyncScope();
			IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

			try
			{
				Result<Guid, AppException> result = await mediator.Send(request: new CreateTransactionCommand(
					AccountId: accountId,
					UserId: userId,
					CategoryId: categoryId,
					Amount: 100m,
					Currency: Currency.Create(value: "RUB").Value,
					Direction: DirectionType.Credit,
					Description: null,
					OccurredAt: DateTimeOffset.UtcNow
				)
				{ IdempotencyKey = Guid.CreateVersion7() });

				return result.IsFailure ? result.Error : null;
			}
			catch (AppException exception)
			{
				return exception;
			}
		});

		AppException?[] outcomes = await Task.WhenAll(tasks: attempts);

		foreach (AppException? outcome in outcomes)
		{
			if (outcome is null)
				continue;

			DefaultHttpContext context = await AnswerForAsync(error: outcome);

			await Assert.That(value: context.Response.StatusCode).IsLessThan(maximum: StatusCodes.Status500InternalServerError).Because(message: $"""
				Losing a version race is an ordinary outcome of contention, not a fault: the caller repeats
				the request and it goes through. {outcome.GetType().Name} came back instead with a status
				that tells them the server broke, which is both wrong and unactionable.
			""");
		}
	}
}

public sealed class RateLimitFailureSurfaceTests : MediatorFixture
{
	protected override IReadOnlyDictionary<string, string?> ConfigurationOverrides { get; } = new Dictionary<string, string?>
	{
		["RateLimit:RequestsPerWindow"] = "1",
		["RateLimit:WindowSeconds"] = "60"
	};

	private UserBuilder _userBuilder = null!;

	[Before(hookType: Test)]
	public async Task SetupDataAsync()
	{
		_userBuilder = new UserBuilder(context: Context);
		await new CurrencyBuilder(context: Context).CreateAsync(code: "RUB");
	}

	private CreateAccountCommand Command(Guid userId) => new CreateAccountCommand(
		UserId: userId,
		Name: Name.Create(value: "Основной счёт").Value,
		Type: AccountType.Checking,
		Currency: Currency.Create(value: "RUB").Value,
		InitialBalance: 0m
	)
	{ IdempotencyKey = Guid.CreateVersion7() };

	[Test]
	public async Task ACommandPastThePerUserCeiling_ShouldAnswer429WithRetryAfter()
	{
		Guid userId = await _userBuilder.CreateAsync();

		const int attempts = 6;
		AppException? refusal = null;

		for (int attempt = 0; attempt < attempts && refusal is null; attempt++)
		{
			Result<Guid, AppException> result = await Mediator.Send(request: Command(userId: userId));

			if (result.IsFailure)
				refusal = result.Error;
		}

		await Assert.That(value: refusal).IsTypeOf<RateLimitExceededException>().Because(message: """
			The ceiling is one request per window, so everything after the first should be refused.
			Several are attempted because FallbackRateLimiter hands a request to the in-memory limiter
			when Redis misses its probe budget, and clears those counts once Redis answers again — one
			admitted request past the ceiling is that handover, not a limit that failed to apply.
		""");

		DefaultHttpContext context = await FailureSurfaceTests.AnswerForAsync(error: refusal!);

		await Assert.That(value: context.Response.StatusCode).IsEqualTo(expected: StatusCodes.Status429TooManyRequests);

		await Assert.That(value: context.Response.Headers.RetryAfter.ToString()).IsNotEmpty().Because(message: """
			Without Retry-After a client has nothing to schedule against and will hammer the limit until
			the window happens to roll. The header is the whole difference between a limit that sheds load
			and one that multiplies it.
		""");
	}
}
