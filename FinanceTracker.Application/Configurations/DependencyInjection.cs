using FinanceTracker.Application.Behaviours;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application.Configurations;

public static class DependencyInjection
{
	public static IServiceCollection AddApplication(this IServiceCollection services)
	{
		services.AddMediatR(configuration: configuration =>
		{
			configuration.RegisterServicesFromAssembly(assembly: typeof(DependencyInjection).Assembly);
			configuration.AddBehavior(serviceType: typeof(IPipelineBehavior<,>), implementationType: typeof(ValidationBehaviours<,>));
		});
		
		services.AddValidatorsFromAssembly(assembly: typeof(DependencyInjection).Assembly);
		
		return services;
	}
}