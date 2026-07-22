using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Worker.Shared.Projection;

public static class ProjectionRetryOptionsExtensions
{
	public static IServiceCollection AddProjectionRetryOptions(this IServiceCollection services)
	{
		services.AddOptions<ProjectionRetryOptions>()
			.BindConfiguration(configSectionPath: ProjectionRetryOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}
}
