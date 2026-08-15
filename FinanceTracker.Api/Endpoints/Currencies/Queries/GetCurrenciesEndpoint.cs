using FinanceTracker.Api.Endpoints.Currencies.Contracts;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Currency.Queries.GetCurrencies;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Currency;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Currencies.Queries;

public sealed class GetCurrenciesEndpoint : IEndpoint
{
	public string GroupName => CurrenciesEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/", handler: HandleAsync)
			.RequirePermission(resource: Resource.Currency, action: PermissionAction.Read)
			.WithSummary(summary: "List supported currencies")
			.WithDescription(description:
				"Reference data shared by every user. Includes currencies no longer in circulation, " +
				"flagged as inactive, so an account denominated in one can still be rendered."
			).Produces<IReadOnlyList<CurrencyResponse>>(statusCode: StatusCodes.Status200OK);
	}

	private static async Task<IHttpResult> HandleAsync(
		ISender sender,
		CancellationToken ct)
	{
		Result<IReadOnlyList<CurrencyInfo>, AppException> result = await sender.Send(
			request: new GetCurrenciesQuery(),
			cancellationToken: ct
		);

		return result.ToHttpResult<CurrencyInfo, CurrencyResponse>();
	}
}
