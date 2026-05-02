using FinanceTracker.Application.Accounts.Commands.ArchiveAccount;
using FinanceTracker.Application.Accounts.Commands.RenameAccount;
using FinanceTracker.Application.Accounts.Commands.UnarchiveAccount;
using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application.Accounts.Authorization;

internal static class AccountHandlerRegistration
{
	internal static IServiceCollection RegisterAccountHandlers(this IServiceCollection services)
	{
		services.AddScoped<AccountLoader>();
		services.AddScoped<IEntityLoader<ArchiveAccountCommand, Account>>(sp => sp.GetRequiredService<AccountLoader>());
		services.AddScoped<IEntityLoader<UnarchiveAccountCommand, Account>>(sp => sp.GetRequiredService<AccountLoader>());
		services.AddScoped<IEntityLoader<RenameAccountCommand, Account>>(sp => sp.GetRequiredService<AccountLoader>());

		services.AddScoped<IAuthorizedHandler<ArchiveAccountCommand, Account>, ArchiveAccountHandler>();
		services.AddScoped<IAuthorizedHandler<UnarchiveAccountCommand, Account>, UnarchiveAccountHandler>();
		services.AddScoped<IAuthorizedHandler<RenameAccountCommand, Account>, RenameAccountHandler>();

		services.AddScoped<IRequestHandler<ArchiveAccountCommand>, AuthorizedHandlerAdapter<ArchiveAccountCommand, Account>>();
		services.AddScoped<IRequestHandler<UnarchiveAccountCommand>, AuthorizedHandlerAdapter<UnarchiveAccountCommand, Account>>();
		services.AddScoped<IRequestHandler<RenameAccountCommand>, AuthorizedHandlerAdapter<RenameAccountCommand, Account>>();

		return services;
	}
}