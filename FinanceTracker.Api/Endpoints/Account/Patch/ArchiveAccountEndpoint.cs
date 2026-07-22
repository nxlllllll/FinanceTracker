using FinanceTracker.Api.Infrastructure;
using FinanceTracker.Application.UseCases.Account.Commands.ArchiveAccount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.Account.Patch;

public sealed class ArchiveAccountEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPatch(pattern: "/api/v1/accounts/{accountId:guid}/archive", handler: HandleAsync)
			.RequireAuthorization()
			.WithTags(tags: "Accounts")
			.WithSummary(summary: "Archive an account")
			.Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid accountId,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		ArchiveAccountCommand command = new ArchiveAccountCommand(
			UserId: currentUser.UserId,
			AccountId: accountId
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
