using FinanceTracker.Api.Routing;

namespace FinanceTracker.Api.Endpoints.Roles;

public sealed class RolesEndpointGroup : IEndpointGroup
{
	public const string GroupName = "Roles";

	public string Name => GroupName;
	public string Prefix => "/roles";

	public void Configure(RouteGroupBuilder group) => group.WithTags(tags: Name);
}
