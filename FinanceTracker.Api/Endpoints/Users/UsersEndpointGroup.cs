using FinanceTracker.Api.Routing;

namespace FinanceTracker.Api.Endpoints.Users;

public sealed class UsersEndpointGroup : IEndpointGroup
{
	public const string GroupName = "Users";

	public string Name => GroupName;
	public string Prefix => "/users";

	public void Configure(RouteGroupBuilder group) => group.WithTags(tags: Name);
}
