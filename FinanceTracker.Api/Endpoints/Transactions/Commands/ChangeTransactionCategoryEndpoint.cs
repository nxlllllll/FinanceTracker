using FinanceTracker.Api.Endpoints.Transactions.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionCategory;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Transactions.Commands;

public sealed class ChangeTransactionCategoryEndpoint : IEndpoint
{
	public string GroupName => TransactionsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPatch(pattern: "/{transactionId:guid}/category", handler: HandleAsync)
			.RequirePermission(resource: Resource.Transaction, action: PermissionAction.Write)
			.WithSummary(summary: "Move a transaction to another category")
			.WithDescription(description:
				"Both categories' monthly totals are adjusted in the same transaction, so analytics never sees the amount counted twice or not at all. " +
				"The target must be of the same direction as the transaction and must not be archived."
			).Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	internal static async Task<IHttpResult> HandleAsync(
		Guid transactionId,
		ChangeTransactionCategoryRequest request,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		ChangeTransactionCategoryCommand command = new ChangeTransactionCategoryCommand(
			UserId: currentUser.UserId,
			TransactionId: transactionId,
			CategoryId: request.CategoryId
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
