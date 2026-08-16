using FinanceTracker.Api.Endpoints.Transactions.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionDescription;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Transactions.Commands;

public sealed class ChangeTransactionDescriptionEndpoint : IEndpoint
{
	public string GroupName => TransactionsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPatch(pattern: "/{transactionId:guid}/description", handler: HandleAsync)
			.RequirePermission(resource: Resource.Transaction, action: PermissionAction.Write)
			.WithSummary(summary: "Change a transaction's description")
			.WithDescription(description: "Send null to clear it. The description is a note for the person and takes no part in analytics.")
			.Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid transactionId,
		ChangeTransactionDescriptionRequest request,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		ChangeTransactionDescriptionCommand command = new ChangeTransactionDescriptionCommand(
			UserId: currentUser.UserId,
			TransactionId: transactionId,
			Description: request.Description
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
