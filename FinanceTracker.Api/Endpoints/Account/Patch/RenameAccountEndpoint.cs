using FinanceTracker.Api.Contracts.Account.Request;
using FinanceTracker.Api.Infrastructure;
using FinanceTracker.Application.UseCases.Account.Commands.RenameAccount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.Account.Patch;

public sealed class RenameAccountEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPatch(pattern: "/api/v1/accounts/{accountId:guid}/rename", handler: HandleAsync)
			.RequireAuthorization()
			.WithTags(tags: "Accounts")
			.WithSummary(summary: "Rename an account")
			.Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid accountId,
		RenameAccountRequest request,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		Result<Name, DomainException> newName = Name.Create(value: request.NewName);
		if (newName.IsFailure)
			return newName.Error!.ToValidationProblem();

		RenameAccountCommand command = new RenameAccountCommand(
			UserId: currentUser.UserId,
			AccountId: accountId,
			NewName: newName.Value!
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
