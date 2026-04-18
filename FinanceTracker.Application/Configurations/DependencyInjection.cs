using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application.Configurations;

public static class DependencyInjection
{
	public static IServiceCollection AddApplication(this IServiceCollection services)
	{
		services.AddMediatR(configuration: configuration =>
			configuration.RegisterServicesFromAssembly(assembly: typeof(DependencyInjection).Assembly)
		);
		return services;
	}
}