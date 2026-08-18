using FinanceTracker.Api.Routing;

namespace FinanceTracker.Api.Endpoints.Transfers;

public sealed class TransfersEndpointGroup : IEndpointGroup
{
	public const string GroupName = "Transfers";

	public string Name => GroupName;
	public string Prefix => "/transfers";

	public void Configure(RouteGroupBuilder group) => group.WithTags(tags: Name);
}
