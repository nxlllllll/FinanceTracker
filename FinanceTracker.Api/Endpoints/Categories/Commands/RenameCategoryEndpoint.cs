using FinanceTracker.Api.Endpoints.Categories.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Category.Commands.RenameCategory;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Categories.Commands;

public sealed class RenameCategoryEndpoint : IEndpoint
{
	public string GroupName => CategoriesEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPatch(pattern: "/{categoryId:guid}/name", handler: HandleAsync)
			.RequirePermission(resource: Resource.Category, action: PermissionAction.Write)
			.WithSummary(summary: "Rename a category")
			.WithDescription(description: "Renaming an archived category is refused.")
			.Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid categoryId,
		RenameCategoryRequest request,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		Result<Name, DomainException> name = Name.Create(value: request.Name);
		if (name.IsFailure)
			return name.Error!.ToValidationProblem(fieldName: nameof(request.Name));

		RenameCategoryCommand command = new RenameCategoryCommand(
			UserId: currentUser.UserId,
			CategoryId: categoryId,
			NewName: name.Value!
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
