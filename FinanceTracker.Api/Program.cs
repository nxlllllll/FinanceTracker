using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceTracker.Api.Configurations;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Middleware;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.Configurations;
using FinanceTracker.Infrastructure.Configurations;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

namespace FinanceTracker.Api;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);

		builder.AddStructuredLogging();

		builder.Host.UseDefaultServiceProvider(configure: options =>
		{
			options.ValidateScopes = true;
			options.ValidateOnBuild = true;
		});

		builder.Services.AddApplication();
		builder.Services.AddPersistence(configuration: builder.Configuration);
		builder.Services.AddAuth();
		builder.Services.AddApiTelemetry();

		builder.Services.AddInfrastructureHealthChecks(
			connectionString: builder.Configuration.RequireValue(path: "ConnectionStrings:FinanceTrackerContext"),
			redisConnectionString: builder.Configuration.RequireValue(path: "Redis:ConnectionString")
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

		builder.Services.AddOptions<ProxyOptions>()
			.BindConfiguration(configSectionPath: ProxyOptions.SectionName)
			.ValidateOnStart();
		builder.Services.AddSingleton<IValidateOptions<ProxyOptions>, ProxyOptionsValidator>();
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

		if (!builder.Environment.IsDevelopment())
		{
			builder.Services.AddHsts(configureOptions: options => options.MaxAge = TimeSpan.FromDays(value: 365));
		}

		builder.WebHost.ConfigureKestrel(options: kestrel =>
		{
			kestrel.ListenAnyIP(port: ApiPorts.Public);
			kestrel.ListenAnyIP(port: ApiPorts.Observability);
		});

		WebApplication app = builder.Build();

		app.UseForwardedHeaders();

		app.UseCorrelationIdMiddleware();

		if (!app.Environment.IsDevelopment())
			app.UseHsts();

		app.UseSecurityHeaders();

		app.UseExceptionHandler();

		app.UseAuthentication();
		app.UseAuthorization();

		app.MapHealthChecks(pattern: "/health/live", options: new HealthCheckOptions
		{
			Predicate = _ => false
		}).RequireHost(hosts: ApiPorts.ObservabilityHost);

		app.MapHealthChecks(pattern: "/health/ready", options: new HealthCheckOptions
		{
			Predicate = check => check.Tags.Contains(item: "ready"),
			ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
		}).RequireHost(hosts: ApiPorts.ObservabilityHost);

		app.UseWhen(
			predicate: context => context.Connection.LocalPort == ApiPorts.Observability,
			configuration: branch => branch.UseHealthChecksPrometheusExporter(
				endpoint: "/health/metrics",
				configure: options => options.ResultStatusCodes[HealthStatus.Unhealthy] = (int)HttpStatusCode.OK
			)
		);

		app.MapPrometheusScrapingEndpoint().RequireHost(hosts: ApiPorts.ObservabilityHost);

		app.MapEndpoints();

		if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>(key: "OpenApi:Expose"))
		{
			app.MapOpenApi();
			app.MapScalarApiReference(configureOptions: options =>
			{
				options.WithTitle(title: "FinanceTracker API").WithDefaultHttpClient(target: ScalarTarget.CSharp, client: ScalarClient.HttpClient);
			});

			// Only meaningful when the reference is actually mapped. Previously this sat outside the
			// check, so the root of a Production deployment redirected to a 404.
			app.MapGet(pattern: "/", handler: () => Results.Redirect(url: "/scalar/v1")).ExcludeFromDescription();
		}

		app.Run();
	}
}
