using FinanceTracker.Api.Endpoints.Accounts.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Filters;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Account.Commands.RenameAccount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Accounts.Commands;

public sealed class RenameAccountEndpoint : IEndpoint
{
	public string GroupName => AccountsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPatch(pattern: "/{accountId:guid}/rename", handler: HandleAsync)
			.RequirePermission(resource: Resource.Account, action: PermissionAction.Write)
			.AcceptsIfMatch()
			.WithSummary(summary: "Rename an account")
			.WithDescription(description: "Send an If-Match header (from a prior GET's ETag) to reject the request with 412 if the account changed since you fetched it.")
			.Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status403Forbidden)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status412PreconditionFailed);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid accountId,
		RenameAccountRequest request,
		HttpContext httpContext,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		Result<Name, DomainException> newName = Name.Create(value: request.NewName);
		if (newName.IsFailure)
			return newName.Error!.ToValidationProblem(fieldName: nameof(request.NewName));

		RenameAccountCommand command = new RenameAccountCommand(
			UserId: currentUser.UserId,
			AccountId: accountId,
			NewName: newName.Value)
		{
			ExpectedVersion = ETag.ToVersion(ifMatchHeaderValue: httpContext.Request.Headers.IfMatch)
		};

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
