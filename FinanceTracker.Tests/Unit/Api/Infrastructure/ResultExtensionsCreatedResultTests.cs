using FinanceTracker.Api.Http.Results;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Tests.Unit.Api.Infrastructure;

public sealed class ResultExtensionsCreatedResultTests
{
	private static async Task<int> ExecuteAndReadStatusAsync(IResult result)
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

		return context.Response.StatusCode;
	}

	[Test]
	public async Task ToCreatedResult_OnFailure_ShouldNotReportSuccess()
	{
		Result<Guid, AppException> failure = Result<Guid, AppException>.Failure(
			error: new NotFoundException(message: "Account not found.", id: Guid.CreateVersion7())
		);

		int statusCode = await ExecuteAndReadStatusAsync(result: failure.ToCreatedResult(
			locationFactory: id => $"/api/v1/accounts/{id}"
		));

		await Assert.That(value: statusCode).IsEqualTo(expected: StatusCodes.Status404NotFound).Because(message: """
			A failed Result must never leave this method as a 2xx. If this starts failing with 201, the
			failure branch has lost its return statement again.
		""");
	}

	[Test]
	public async Task ToCreatedResult_OnSuccess_ShouldReturnCreatedWithTheId()
	{
		Guid accountId = Guid.CreateVersion7();
		Result<Guid, AppException> success = Result<Guid, AppException>.Success(value: accountId);

		int statusCode = await ExecuteAndReadStatusAsync(result: success.ToCreatedResult(
			locationFactory: id => $"/api/v1/accounts/{id}"
		));

		await Assert.That(value: statusCode).IsEqualTo(expected: StatusCodes.Status201Created);
	}
}
