using FinanceTracker.Api.Routing;

namespace FinanceTracker.Api.Endpoints.Currencies;

public sealed class CurrenciesEndpointGroup : IEndpointGroup
{
	public const string GroupName = "Currencies";

	public string Name => GroupName;
	public string Prefix => "/currencies";

	public void Configure(RouteGroupBuilder group) => group.WithTags(tags: Name);
}
