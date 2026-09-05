using FinanceTracker.Api.Endpoints.Categories.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Category.Commands.ChangeCategoryParent;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Categories.Commands;

public sealed class ChangeCategoryParentEndpoint : IEndpoint
{
	public string GroupName => CategoriesEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPatch(pattern: "/{categoryId:guid}/parent", handler: HandleAsync)
			.RequirePermission(resource: Resource.Category, action: PermissionAction.Write)
			.WithSummary(summary: "Move a category under another one")
			.WithDescription(description:
				"The subtree moves with it — children point at this category, not at its former parent. " +
				"Send parentId null to make it a root. Refused when the new parent is the category itself " +
				"or one of its own descendants, when the resulting tree would be deeper than the ceiling, " +
				"when the new parent records the other kind of operation, or when either side is archived."
			).Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	internal static async Task<IHttpResult> HandleAsync(
		Guid categoryId,
		ChangeCategoryParentRequest request,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		ChangeCategoryParentCommand command = new ChangeCategoryParentCommand(
			UserId: currentUser.UserId,
			CategoryId: categoryId,
			NewParentId: request.ParentId
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
