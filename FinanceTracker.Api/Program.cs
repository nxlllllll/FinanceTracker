using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceTracker.Api.Auth;
using FinanceTracker.Api.Endpoints;
using FinanceTracker.Api.Infrastructure;
using FinanceTracker.Application.Configurations;
using FinanceTracker.Infrastructure.Configurations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

namespace FinanceTracker.Api;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);

		builder.Host.UseDefaultServiceProvider(configure: options =>
		{
			options.ValidateScopes = true;
			options.ValidateOnBuild = true;
		});

		builder.Services.AddApplication();
		builder.Services.AddPersistence(configuration: builder.Configuration);
		builder.Services.AddAuth();

		builder.Services.AddInfrastructureHealthChecks(
			connectionString: builder.Configuration.GetConnectionString(name: "FinanceTrackerContext")!,
			redisConnectionString: builder.Configuration.GetSection(key: "Redis")["ConnectionString"]!
		);

		builder.Services.AddEndpoints();

		builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
		builder.Services.AddProblemDetails();

		builder.Services.AddAuthentication(defaultScheme: JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
		builder.Services.ConfigureOptions<JwtBearerOptionsSetup>();
		builder.Services.AddAuthorizationBuilder().AddPolicy(
			name: AuthorizationExtensions.RootPolicyName,
			configurePolicy: policy => policy.AddRequirements(requirements: new RootRequirement())
		);
		builder.Services.AddScoped<IAuthorizationHandler, RootAuthorizationHandler>();
		builder.Services.AddHttpContextAccessor();
		builder.Services.AddScoped<ICurrentUserProvider, CurrentUserProvider>();

		builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
		builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
		builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ForbiddenProblemDetailsAuthorizationMiddlewareResultHandler>();

		builder.Services.Configure<ProxyOptions>(config: builder.Configuration.GetSection(key: "Proxy"));
		builder.Services.ConfigureOptions<ForwardedHeadersOptionsSetup>();

		builder.Services.AddOpenApi(configureOptions: options =>
		{
			options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
		});

		builder.Services.ConfigureHttpJsonOptions(configureOptions: options =>
		{
			options.SerializerOptions.Converters.Add(item: new JsonStringEnumConverter(namingPolicy: JsonNamingPolicy.CamelCase));
			options.SerializerOptions.WriteIndented = builder.Environment.IsDevelopment();
		});

		WebApplication app = builder.Build();

		app.UseForwardedHeaders();

		app.UseCorrelationIdMiddleware();

		app.UseExceptionHandler();

		app.UseAuthentication();
		app.UseAuthorization();

		app.MapHealthChecks(pattern: "/health/live", options: new HealthCheckOptions
		{
			Predicate = _ => false
		});

		app.MapHealthChecks(pattern: "/health/ready", options: new HealthCheckOptions
		{
			Predicate = check => check.Tags.Contains(item: "ready")
		});

		app.MapEndpoints();

		if (app.Environment.IsDevelopment())
		{
			app.MapOpenApi();
			app.MapScalarApiReference(configureOptions: options =>
			{
				options.WithTitle(title: "FinanceTracker API").WithDefaultHttpClient(target: ScalarTarget.CSharp, client: ScalarClient.HttpClient);
			});
		}

		app.MapGet(pattern: "/", handler: () => Results.Redirect(url: "/scalar/v1")).ExcludeFromDescription();
		app.Run();
	}
}

