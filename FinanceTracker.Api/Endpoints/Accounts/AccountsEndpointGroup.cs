using FinanceTracker.Api.Routing;

namespace FinanceTracker.Api.Endpoints.Accounts;

public sealed class AccountsEndpointGroup : IEndpointGroup
{
	public const string GroupName = "Accounts";

	public string Name => GroupName;
	public string Prefix => "/accounts";

	public void Configure(RouteGroupBuilder group) => group.WithTags(tags: Name);
}
