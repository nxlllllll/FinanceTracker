using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Category.Commands.UnarchiveCategory;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Categories.Commands;

public sealed class UnarchiveCategoryEndpoint : IEndpoint
{
	public string GroupName => CategoriesEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: "/{categoryId:guid}/unarchive", handler: HandleAsync)
			.RequirePermission(resource: Resource.Category, action: PermissionAction.Write)
			.WithSummary(summary: "Bring an archived category back")
			.WithDescription(description: "Unarchiving one that was never archived succeeds and changes nothing.")
			.Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid categoryId,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		UnarchiveCategoryCommand command = new UnarchiveCategoryCommand(
			UserId: currentUser.UserId,
			CategoryId: categoryId
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
