using FinanceTracker.Api.Endpoints.Users.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Application.UseCases.User.Commands.ChangeUserBaseCurrency;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Users.Commands;

public sealed class ChangeBaseCurrencyEndpoint : IEndpoint
{
	public string GroupName => UsersEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPatch(pattern: "/me/base-currency", handler: HandleAsync)
			.RequireAuthorization()
			.WithSummary(summary: "Change the currency everything is summed in")
			.WithDescription(description:
				"Individual accounts keep their own currencies; this only changes what they are compared in. Every " +
				"category total and summary is rebuilt in the background against the new one — until that finishes " +
				"those figures answer with recalculationPending set, and are still denominated the old way."
			).Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	private static async Task<IHttpResult> HandleAsync(
		ChangeBaseCurrencyRequest request,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		Result<Currency, DomainException> currency = Currency.Create(value: request.BaseCurrency);
		if (currency.IsFailure)
			return currency.Error!.ToValidationProblem(fieldName: nameof(request.BaseCurrency));

		ChangeUserBaseCurrencyCommand command = new ChangeUserBaseCurrencyCommand(
			UserId: currentUser.UserId,
			NewBaseCurrency: currency.Value
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
