namespace FinanceTracker.Api.Routing;

public interface IEndpoint
{
	/// <summary>
	/// The <see cref="IEndpointGroup.Name"/> this endpoint belongs to.
	/// Determines the prefix its route hangs off and the metadata it inherits.
	/// </summary>
	string GroupName { get; }

	/// <summary>
	/// Registers the route(s) of this endpoint on its group's route builder.
	/// </summary>
	void MapEndpoint(IEndpointRouteBuilder group);
}
