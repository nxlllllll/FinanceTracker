using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceTracker.Api.Auth;
using FinanceTracker.Api.Endpoints;
using FinanceTracker.Api.Infrastructure;
using FinanceTracker.Application.Configurations;
using FinanceTracker.Infrastructure.Configurations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

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

		builder.Services.AddHealthChecks();

		builder.Services.AddEndpoints();

		builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
		builder.Services.AddProblemDetails();

		builder.Services.AddAuthentication(defaultScheme: JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
		builder.Services.ConfigureOptions<JwtBearerOptionsSetup>();
		builder.Services.AddAuthorization();
		builder.Services.AddHttpContextAccessor();
		builder.Services.AddScoped<ICurrentUserProvider, CurrentUserProvider>();

		builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
		builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
		builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ForbiddenProblemDetailsAuthorizationMiddlewareResultHandler>();

		builder.Services.ConfigureHttpJsonOptions(configureOptions: options =>
		{
			options.SerializerOptions.Converters.Add(item: new JsonStringEnumConverter(namingPolicy: JsonNamingPolicy.CamelCase));
			options.SerializerOptions.WriteIndented = builder.Environment.IsDevelopment();
		});

		WebApplication app = builder.Build();

		app.UseExceptionHandler();

		app.UseAuthentication();
		app.UseAuthorization();

		app.MapHealthChecks(pattern: "/health/live");

		app.MapEndpoints();

		app.Run();
	}
}

