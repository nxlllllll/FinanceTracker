using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Users.Commands.ChangeUserBaseCurrency;
using FinanceTracker.Application.UseCases.Users.Commands.ChangeUserEmail;
using FinanceTracker.Application.UseCases.Users.Commands.ChangeUserPassword;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application.UseCases.Users.Authorization;

internal static class UserHandlerRegistration
{
	internal static IServiceCollection RegisterUserHandlers(this IServiceCollection services)
	{
		services.AddScoped<UserLoader>();
		services.AddScoped<IEntityLoader<ChangeUserBaseCurrencyCommand, User, NotFoundException>>(sp => sp.GetRequiredService<UserLoader>());
		services.AddScoped<IEntityLoader<ChangeUserEmailCommand, User, NotFoundException>>(sp => sp.GetRequiredService<UserLoader>());
		services.AddScoped<IEntityLoader<ChangeUserPasswordCommand, User, NotFoundException>>(sp => sp.GetRequiredService<UserLoader>());

		services.AddScoped<IAuthorizedHandler<ChangeUserBaseCurrencyCommand, User, Guid, DomainException>, ChangeUserBaseCurrencyHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeUserEmailCommand, User, Guid, DomainException>, ChangeUserEmailHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeUserPasswordCommand, User, Guid, DomainException>, ChangeUserPasswordHandler>();

		services.AddScoped<IRequestHandler<ChangeUserBaseCurrencyCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<ChangeUserBaseCurrencyCommand, User, Guid, DomainException>>();
		services.AddScoped<IRequestHandler<ChangeUserEmailCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<ChangeUserEmailCommand, User, Guid, DomainException>>();
		services.AddScoped<IRequestHandler<ChangeUserPasswordCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<ChangeUserPasswordCommand, User, Guid, DomainException>>();

		return services;
	}
}