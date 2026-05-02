using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Users.Commands.ChangeUserBaseCurrency;
using FinanceTracker.Application.Users.Commands.ChangeUserEmail;
using FinanceTracker.Application.Users.Commands.ChangeUserPassword;
using FinanceTracker.Core.Domains.User;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application.Users.Authorization;

internal static class UserHandlerRegistration
{
	internal static IServiceCollection RegisterUserHandlers(this IServiceCollection services)
	{
		services.AddScoped<UserLoader>();
		services.AddScoped<IEntityLoader<ChangeUserBaseCurrencyCommand, User>>(sp => sp.GetRequiredService<UserLoader>());
		services.AddScoped<IEntityLoader<ChangeUserEmailCommand, User>>(sp => sp.GetRequiredService<UserLoader>());
		services.AddScoped<IEntityLoader<ChangeUserPasswordCommand, User>>(sp => sp.GetRequiredService<UserLoader>());

		services.AddScoped<IAuthorizedHandler<ChangeUserBaseCurrencyCommand, User>, ChangeUserBaseCurrencyHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeUserEmailCommand, User>, ChangeUserEmailHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeUserPasswordCommand, User>, ChangeUserPasswordHandler>();

		services.AddScoped<IRequestHandler<ChangeUserBaseCurrencyCommand>, AuthorizedHandlerAdapter<ChangeUserBaseCurrencyCommand, User>>();
		services.AddScoped<IRequestHandler<ChangeUserEmailCommand>, AuthorizedHandlerAdapter<ChangeUserEmailCommand, User>>();
		services.AddScoped<IRequestHandler<ChangeUserPasswordCommand>, AuthorizedHandlerAdapter<ChangeUserPasswordCommand, User>>();

		return services;
	}
}