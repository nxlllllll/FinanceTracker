using FinanceTracker.Api.Routing;

namespace FinanceTracker.Api.Endpoints.RecurringTransactions;

public sealed class RecurringTransactionsEndpointGroup : IEndpointGroup
{
	public const string GroupName = "RecurringTransactions";

	public string Name => GroupName;
	public string Prefix => "/recurring-transactions";

	public void Configure(RouteGroupBuilder group) => group.WithTags(tags: Name);
}
