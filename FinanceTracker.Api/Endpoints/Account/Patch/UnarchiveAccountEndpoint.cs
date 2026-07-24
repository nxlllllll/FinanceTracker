using FinanceTracker.Api.Auth;
using FinanceTracker.Api.Infrastructure;
using FinanceTracker.Application.UseCases.Account.Commands.UnarchiveAccount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.Account.Patch;

public sealed class UnarchiveAccountEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPatch(pattern: "/accounts/{accountId:guid}/unarchive", handler: HandleAsync)
			.RequirePermission(resource: Resource.Account, action: PermissionAction.Write)
			.WithTags(tags: "Accounts")
			.WithSummary(summary: "Unarchive an account")
			.WithDescription(description: "Send an If-Match header (from a prior GET's ETag) to reject the request with 412 if the account changed since you fetched it.")
			.Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesProblem(statusCode: StatusCodes.Status403Forbidden)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status412PreconditionFailed);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid accountId,
		HttpContext httpContext,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		UnarchiveAccountCommand command = new UnarchiveAccountCommand(
			UserId: currentUser.UserId,
			AccountId: accountId)
		{
			ExpectedVersion = ETag.ToVersion(ifMatchHeaderValue: httpContext.Request.Headers.IfMatch)
		};

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
