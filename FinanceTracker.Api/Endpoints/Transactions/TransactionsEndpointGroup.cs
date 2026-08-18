using FinanceTracker.Api.Routing;

namespace FinanceTracker.Api.Endpoints.Transactions;

public sealed class TransactionsEndpointGroup : IEndpointGroup
{
	public const string GroupName = "Transactions";

	public string Name => GroupName;
	public string Prefix => "/transactions";

	public void Configure(RouteGroupBuilder group) => group.WithTags(tags: Name);
}
