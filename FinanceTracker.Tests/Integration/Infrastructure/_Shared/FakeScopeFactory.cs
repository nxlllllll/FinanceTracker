using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Integration.Infrastructure._Shared;

public sealed class FakeScopeFactory(
	IServiceProvider serviceProvider
) : IServiceScopeFactory
{
	public IServiceScope CreateScope()
		=> serviceProvider.CreateScope();
}