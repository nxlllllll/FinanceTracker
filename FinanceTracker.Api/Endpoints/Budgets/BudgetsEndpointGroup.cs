using FinanceTracker.Api.Routing;

namespace FinanceTracker.Api.Endpoints.Budgets;

public sealed class BudgetsEndpointGroup : IEndpointGroup
{
	public const string GroupName = "Budgets";

	public string Name => GroupName;
	public string Prefix => "/budgets";

	public void Configure(RouteGroupBuilder group) => group.WithTags(tags: Name);
}
