using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinanceTracker.Worker.Shared.Host;

/// <summary>
/// Extension methods for hardening the DI container each worker builds at startup.
/// </summary>
public static class WorkerHostValidationExtensions
{
	/// <summary>
	/// Enables strict DI container validation via <see cref="ServiceProviderOptions"/>:
	/// <list type="bullet">
	/// <item><c>ValidateOnBuild</c> — fails <see cref="WebApplicationBuilder.Build"/> immediately if any
	/// registration in the container cannot be resolved, instead of surfacing the error later on first use.</item>
	/// <item><c>ValidateScopes</c> — fails immediately if a scoped or transient service is captured by a
	/// singleton ("captive dependency"). Without this, the DI container silently resolves the scoped
	/// service once and pins that single instance (e.g. one <c>DbContext</c>) for the lifetime of the
	/// singleton — which for a non-thread-safe dependency like EF Core's <c>DbContext</c> causes data
	/// corruption or <c>InvalidOperationException</c> under concurrent load, not a clean startup failure.</item>
	/// </list>
	/// </summary>
	public static WebApplicationBuilder UseStrictDependencyValidation(this WebApplicationBuilder builder)
	{
		builder.Host.UseDefaultServiceProvider(configure: options =>
		{
			options.ValidateScopes = true;
			options.ValidateOnBuild = true;
		});

		return builder;
	}
}
