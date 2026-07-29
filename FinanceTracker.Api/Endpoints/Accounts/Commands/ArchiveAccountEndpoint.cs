using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Account.Commands.ArchiveAccount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Accounts.Commands;

public sealed class ArchiveAccountEndpoint : IEndpoint
{
	public string GroupName => AccountsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPatch(pattern: "/{accountId:guid}/archive", handler: HandleAsync)
			.RequirePermission(resource: Resource.Account, action: PermissionAction.Write)
			.WithSummary(summary: "Archive an account")
			.WithDescription(description: "Send an If-Match header (from a prior GET's ETag) to reject the request with 412 if the account changed since you fetched it.")
			.Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesProblem(statusCode: StatusCodes.Status403Forbidden)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status412PreconditionFailed)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid accountId,
		HttpContext httpContext,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		ArchiveAccountCommand command = new ArchiveAccountCommand(
			UserId: currentUser.UserId,
			AccountId: accountId)
		{
			ExpectedVersion = ETag.ToVersion(ifMatchHeaderValue: httpContext.Request.Headers.IfMatch)
		};

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
