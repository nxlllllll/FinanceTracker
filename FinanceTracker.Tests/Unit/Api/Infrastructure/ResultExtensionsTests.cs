using FinanceTracker.Api.Http.Results;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Unit.Api.Infrastructure;

public sealed class ResultExtensionsTests
{
	private sealed class UntaggedTestException(string message) : DomainException(message: message);

	private static async Task<ProblemDetails> ExecuteAndReadProblemAsync(IResult result)
	{
		ServiceCollection services = new ServiceCollection();
		services.AddLogging();
		services.AddProblemDetails();
		IServiceProvider serviceProvider = services.BuildServiceProvider();

		DefaultHttpContext context = new DefaultHttpContext
		{
			RequestServices = serviceProvider,
			Response = { Body = new MemoryStream() }
		};

		await result.ExecuteAsync(httpContext: context);

		context.Response.Body.Seek(offset: 0, origin: SeekOrigin.Begin);
		return (await System.Text.Json.JsonSerializer.DeserializeAsync<ProblemDetails>(utf8Json: context.Response.Body))!;
	}

	[Test]
	public async Task ToProblem_ForTaggedException_ShouldIncludeItsDeclaredCode()
	{
		NotFoundException error = new NotFoundException(message: "Account not found.", id: Guid.CreateVersion7());

		ProblemDetails problem = await ExecuteAndReadProblemAsync(result: error.ToProblem());

		await Assert.That(value: problem.Extensions["code"]!.ToString()).IsEqualTo(expected: "resource.not_found");
	}

	[Test]
	public async Task ToProblem_ForUntaggedException_ShouldFallBackToTheTypeName()
	{
		UntaggedTestException error = new UntaggedTestException(message: "boom");

		ProblemDetails problem = await ExecuteAndReadProblemAsync(result: error.ToProblem());

		await Assert.That(value: problem.Extensions["code"]!.ToString()).IsEqualTo(expected: nameof(UntaggedTestException));
	}

	[Test]
	public async Task ToProblem_ForRateLimitExceededException_ShouldIncludeItsCode()
	{
		RateLimitExceededException error = new RateLimitExceededException(commandName: "TestCommand", retryAfterSeconds: 30);

		ProblemDetails problem = await ExecuteAndReadProblemAsync(result: error.ToProblem());

		await Assert.That(value: problem.Extensions["code"]!.ToString()).IsEqualTo(expected: "rate_limit.exceeded");
	}

	[Test]
	public async Task ToProblem_ForValidationException_ShouldIncludeItsCode()
	{
		ValidationException error = new ValidationException(errors: new Dictionary<string, string[]> { ["name"] = ["Name is required."] });

		ProblemDetails problem = await ExecuteAndReadProblemAsync(result: error.ToProblem());

		await Assert.That(value: problem.Extensions["code"]!.ToString()).IsEqualTo(expected: "validation.failed");
	}

	[Test]
	public async Task ToProblem_ForConcurrencyConflictException_ShouldMapTo409AndIncludeItsCode()
	{
		ConcurrencyConflictException error = new ConcurrencyConflictException(message: "conflict", id: Guid.CreateVersion7());

		IResult result = error.ToProblem();
		ProblemDetails problem = await ExecuteAndReadProblemAsync(result: result);

		await Assert.That(value: problem.Status).IsEqualTo(expected: StatusCodes.Status409Conflict);
		await Assert.That(value: problem.Extensions["code"]!.ToString()).IsEqualTo(expected: "concurrency.conflict");
	}

	[Test]
	public async Task ToProblem_ForPreconditionFailedException_ShouldMapTo412AndIncludeItsCode()
	{
		PreconditionFailedException error = new PreconditionFailedException(
			message: "stale",
			id: Guid.CreateVersion7(),
			expectedVersion: 1,
			actualVersion: 2
		);

		ProblemDetails problem = await ExecuteAndReadProblemAsync(result: error.ToProblem());

		await Assert.That(value: problem.Status).IsEqualTo(expected: StatusCodes.Status412PreconditionFailed);
		await Assert.That(value: problem.Extensions["code"]!.ToString()).IsEqualTo(expected: "concurrency.precondition_failed");
	}
}
