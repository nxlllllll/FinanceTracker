using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Integration.Infrastructure._Shared;

public sealed class FakeScopeFactory(
	IServiceScope scope
) : IServiceScopeFactory
{
	public IServiceScope CreateScope()
		=> scope;
}