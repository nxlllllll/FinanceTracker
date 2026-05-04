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

		services.AddScoped<IAuthorizedHandler<ChangeUserBaseCurrencyCommand, User, Guid>, ChangeUserBaseCurrencyHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeUserEmailCommand, User, Guid>, ChangeUserEmailHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeUserPasswordCommand, User, Guid>, ChangeUserPasswordHandler>();

		services.AddScoped<IRequestHandler<ChangeUserBaseCurrencyCommand, Guid>, AuthorizedHandlerAdapter<ChangeUserBaseCurrencyCommand, User, Guid>>();
		services.AddScoped<IRequestHandler<ChangeUserEmailCommand, Guid>, AuthorizedHandlerAdapter<ChangeUserEmailCommand, User, Guid>>();
		services.AddScoped<IRequestHandler<ChangeUserPasswordCommand, Guid>, AuthorizedHandlerAdapter<ChangeUserPasswordCommand, User, Guid>>();

		return services;
	}
}