using FinanceTracker.Api.Routing;

namespace FinanceTracker.Api.Endpoints.UnresolvableEvents;

public sealed class UnresolvableEventsEndpointGroup : IEndpointGroup
{
	public const string GroupName = "UnresolvableEvents";

	public string Name => GroupName;
	public string Prefix => "/unresolvable-events";

	public void Configure(RouteGroupBuilder group) => group.WithTags(tags: Name);
}
