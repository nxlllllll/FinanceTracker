using System.Text.Json;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Auth;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Currency;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Permission;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Data;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Idempotency;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Exceptions.DomainExceptions.Validation;
using FinanceTracker.Core.Exceptions.TransientExceptions;
using FinanceTracker.Core.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using IResult = Microsoft.AspNetCore.Http.IResult;
using UnitResult = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Tests.Unit.Api.Infrastructure;

public sealed class ResultExtensionsTests
{
	private sealed class UnmappedAppException(string message) : AppException(message: message);

	private sealed class TestTransientException(string message, int retryAfterSeconds)
		: TransientException(message: message, retryAfterSeconds: retryAfterSeconds);

	private sealed record Answer(int StatusCode, IHeaderDictionary Headers, JsonDocument? Body)
	{
		public JsonElement Json => Body?.RootElement ?? throw new InvalidOperationException(message: $"The {StatusCode} answer carried no body to read.");
	}

	private static async Task<Answer> ExecuteAsync(IResult result)
	{
		MemoryStream body = new MemoryStream();

		FeatureCollection features = new FeatureCollection();
		features.Set<IHttpRequestFeature>(instance: new HttpRequestFeature());
		features.Set<IHttpResponseFeature>(instance: new HttpResponseFeature());
		features.Set<IHttpResponseBodyFeature>(instance: new StreamResponseBodyFeature(stream: body));

		DefaultHttpContext context = new DefaultHttpContext(features: features)
		{
			RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
		};

		await result.ExecuteAsync(httpContext: context);

		byte[] written = body.ToArray();

		return new Answer(
			StatusCode: context.Response.StatusCode,
			Headers: context.Response.Headers,
			Body: written.Length == 0 ? null : JsonDocument.Parse(utf8Json: written)
		);
	}

	private static Task<Answer> ProblemFor(AppException error) => ExecuteAsync(result: error.ToProblem());

	[Test]
	[Arguments(400)]
	public async Task AnEmptyIdempotencyKeyIsARequestProblem(int expected)
	{
		Answer answer = await ProblemFor(error: new EmptyIdempotencyKeyException(message: "missing"));

		await Assert.That(value: answer.StatusCode).IsEqualTo(expected: expected);
	}

	[Test]
	public async Task BadCredentialsAndBadTokensAreUnauthorized()
	{
		await Assert.That(value: (await ProblemFor(error: new InvalidCredentialsException())).StatusCode).IsEqualTo(expected: 401);
		await Assert.That(value: (await ProblemFor(error: new InvalidTokenException())).StatusCode).IsEqualTo(expected: 401);
	}

	[Test]
	public async Task MissingThingsAreNotFound()
	{
		await Assert.That(value: (await ProblemFor(error: new NotFoundException(message: "gone", id: Guid.CreateVersion7()))).StatusCode)
			.IsEqualTo(expected: 404);

		await Assert.That(value: (await ProblemFor(error: new CurrencyNotFoundException(message: "no such currency", code: "XXX"))).StatusCode)
			.IsEqualTo(expected: 404)
			.Because(message: "a currency is keyed by code rather than id, but absence is still absence");
	}

	[Test]
	public async Task ClashesAreConflicts()
	{
		await Assert.That(value: (await ProblemFor(error: new ConcurrencyConflictException(message: "stale", id: Guid.CreateVersion7()))).StatusCode)
			.IsEqualTo(expected: 409);
		await Assert.That(value: (await ProblemFor(error: new UniqueConstraintException(message: "dup", constraintName: "uq_x"))).StatusCode)
			.IsEqualTo(expected: 409);
		await Assert.That(value: (await ProblemFor(error: new IdempotencyTimeoutException(message: "waited"))).StatusCode)
			.IsEqualTo(expected: 409);
		await Assert.That(value: (await ProblemFor(error: new IdempotencyAbandonedException(message: "abandoned"))).StatusCode)
			.IsEqualTo(expected: 409);
	}

	[Test]
	public async Task ModifyingYourOwnPermissionsIsForbidden()
	{
		Answer answer = await ProblemFor(error: new SelfPermissionModificationException());

		await Assert.That(value: answer.StatusCode).IsEqualTo(expected: 403)
			.Because(message: "the caller is authenticated and simply not allowed, which is not the same as unauthenticated");
	}

	[Test]
	public async Task AStaleVersionFailsThePrecondition()
	{
		Answer answer = await ProblemFor(error: new PreconditionFailedException(
			message: "etag mismatch",
			id: Guid.CreateVersion7(),
			expectedVersion: 3,
			actualVersion: 5
		));

		await Assert.That(value: answer.StatusCode).IsEqualTo(expected: 412);
	}

	[Test]
	public async Task AnUnlistedDomainRuleIsUnprocessable()
	{
		Answer answer = await ProblemFor(error: new CurrencyException(message: "unsupported"));

		await Assert.That(value: answer.StatusCode).IsEqualTo(expected: 422)
			.Because(message: "a domain rule the client broke is a problem with the request, not with the server");
	}

	[Test]
	public async Task AnythingOutsideTheDomainIsAServerError()
	{
		Answer answer = await ProblemFor(error: new UnmappedAppException(message: "unexpected"));

		await Assert.That(value: answer.StatusCode).IsEqualTo(expected: 500);
	}

	[Test]
	public async Task ARateLimitAnswerCarriesItsRetryDelay()
	{
		Answer answer = await ProblemFor(error: new RateLimitExceededException(commandName: "CreateAccountCommand", retryAfterSeconds: 42));

		await Assert.That(value: answer.StatusCode).IsEqualTo(expected: 429);
		await Assert.That(value: answer.Headers.RetryAfter.ToString()).IsEqualTo(expected: "42")
			.Because(message: "without the header a well-behaved client has to guess when to come back");
	}

	[Test]
	public async Task AMissingPrerequisiteIsTemporaryAndSaysWhenToReturn()
	{
		Answer answer = await ProblemFor(error: new TestTransientException(message: "rate not published yet", retryAfterSeconds: 30));

		await Assert.That(value: answer.StatusCode).IsEqualTo(expected: 503)
			.Because(message: "data the system expects to have later is not a client mistake");

		await Assert.That(value: answer.Headers.RetryAfter.ToString()).IsEqualTo(expected: "30");
	}

	[Test]
	public async Task ValidationFailuresArriveAsAFieldMap()
	{
		Answer answer = await ProblemFor(error: new ValidationException(errors: new Dictionary<string, string[]>
		{
			["name"] = ["Name is required."],
			["amount"] = ["Amount must be positive."]
		}));

		await Assert.That(value: answer.StatusCode).IsEqualTo(expected: 400);

		JsonElement errors = answer.Json.GetProperty(propertyName: "errors");

		await Assert.That(value: errors.TryGetProperty(propertyName: "name", value: out _)).IsTrue();
		await Assert.That(value: errors.TryGetProperty(propertyName: "amount", value: out _)).IsTrue();
	}

	[Test]
	public async Task TheErrorCodeComesFromTheAttributeWhenThereIsOne()
	{
		Answer answer = await ProblemFor(error: new CurrencyNotFoundException(message: "no such currency", code: "XXX"));

		await Assert.That(value: answer.Json.GetProperty(propertyName: "code").GetString())
			.IsEqualTo(expected: "currency.not_found");
	}

	[Test]
	public async Task TheErrorCodeFallsBackToTheTypeName()
	{
		Answer answer = await ProblemFor(error: new UnmappedAppException(message: "unexpected"));

		await Assert.That(value: answer.Json.GetProperty(propertyName: "code").GetString())
			.IsEqualTo(expected: nameof(UnmappedAppException))
			.Because(message: "an unannotated failure still needs something stable for a client to branch on");
	}

	[Test]
	public async Task ASuccessfulCommandWithNothingToReturnIsNoContent()
	{
		Answer answer = await ExecuteAsync(result: Result<UnitResult, AppException>.Success(value: UnitResult.Default).ToNoContentResult());

		await Assert.That(value: answer.StatusCode).IsEqualTo(expected: 204);
		await Assert.That(value: answer.Body).IsNull()
			.Because(message: "204 means there is nothing to read, and a body here would be a contract violation");
	}

	[Test]
	public async Task AFailedCommandWithNothingToReturnStillReportsTheProblem()
	{
		Answer answer = await ExecuteAsync(
			result: Result<UnitResult, AppException>.Failure(error: new NotFoundException(message: "gone", id: Guid.CreateVersion7())).ToNoContentResult()
		);

		await Assert.That(value: answer.StatusCode).IsEqualTo(expected: 404);
	}

	[Test]
	public async Task CreatingAtAnUnmappedRouteFailsLoudly()
	{
		LinkGenerator linkGenerator = new ServiceCollection()
			.AddLogging()
			.AddRouting()
			.BuildServiceProvider()
			.GetRequiredService<LinkGenerator>();

		await Assert.That(action: () => Result<Guid, AppException>.Success(value: Guid.CreateVersion7()).ToCreatedAtRoute(
			linkGenerator: linkGenerator,
			httpContext: new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().AddRouting().BuildServiceProvider() },
			routeName: "NoSuchRoute",
			routeValues: id => new { id }
		)).Throws<InvalidOperationException>();
	}
}
