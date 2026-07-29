namespace FinanceTracker.Api.Routing;

public interface IEndpointGroup
{
	/// <summary>
	/// Matched against <see cref="IEndpoint.GroupName"/>, and used as the OpenAPI tag.
	/// </summary>
	string Name { get; }

	/// <summary>Route prefix, relative to the API root — for example <c>/accounts</c>.</summary>
	string Prefix { get; }

	/// <summary>Applies metadata shared by every endpoint in the group.</summary>
	void Configure(RouteGroupBuilder group);
}
