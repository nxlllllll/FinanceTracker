using FinanceTracker.Api.Routing;

namespace FinanceTracker.Api.Endpoints.Auth;

public sealed class AuthEndpointGroup : IEndpointGroup
{
	public const string GroupName = "Auth";

	public string Name => GroupName;
	public string Prefix => "/auth";

	public void Configure(RouteGroupBuilder group) => group.WithTags(tags: Name);
}
