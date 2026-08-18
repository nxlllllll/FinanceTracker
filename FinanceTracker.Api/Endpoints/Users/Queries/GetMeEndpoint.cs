using FinanceTracker.Api.Endpoints.Users.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Application.UseCases.User.Queries.GetUser;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.User;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Users.Queries;

public sealed class GetMeEndpoint : IEndpoint
{
	public string GroupName => UsersEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/me", handler: HandleAsync)
			.RequireAuthorization()
			.WithSummary(summary: "Get the current user's profile")
			.WithDescription(description: "Reads whoever the token belongs to; there is no route for looking up another user's profile.")
			.Produces<UserResponse>(statusCode: StatusCodes.Status200OK)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	private static async Task<IHttpResult> HandleAsync(
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		Result<UserReadModel, AppException> result = await sender.Send(
			request: new GetUserQuery(UserId: currentUser.UserId),
			cancellationToken: ct
		);

		return result.ToHttpResult<UserReadModel, UserResponse>();
	}
}
