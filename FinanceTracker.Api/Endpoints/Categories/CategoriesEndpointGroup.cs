using FinanceTracker.Api.Routing;

namespace FinanceTracker.Api.Endpoints.Categories;

public sealed class CategoriesEndpointGroup : IEndpointGroup
{
	public const string GroupName = "Categories";

	public string Name => GroupName;
	public string Prefix => "/categories";

	public void Configure(RouteGroupBuilder group) => group.WithTags(tags: Name);
}
