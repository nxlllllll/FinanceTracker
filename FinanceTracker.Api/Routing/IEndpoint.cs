namespace FinanceTracker.Api.Routing;

public interface IEndpoint
{
	/// <summary>Registers the route(s) of this endpoint on the application's route builder.</summary>
	void MapEndpoint(IEndpointRouteBuilder app);
}
