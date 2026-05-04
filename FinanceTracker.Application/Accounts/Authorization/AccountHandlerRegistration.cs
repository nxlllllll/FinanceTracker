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

		services.AddScoped<IAuthorizedHandler<ArchiveAccountCommand, Account, Guid>, ArchiveAccountHandler>();
		services.AddScoped<IAuthorizedHandler<UnarchiveAccountCommand, Account, Guid>, UnarchiveAccountHandler>();
		services.AddScoped<IAuthorizedHandler<RenameAccountCommand, Account, Guid>, RenameAccountHandler>();

		services.AddScoped<IRequestHandler<ArchiveAccountCommand, Guid>, AuthorizedHandlerAdapter<ArchiveAccountCommand, Account, Guid>>();
		services.AddScoped<IRequestHandler<UnarchiveAccountCommand, Guid>, AuthorizedHandlerAdapter<UnarchiveAccountCommand, Account, Guid>>();
		services.AddScoped<IRequestHandler<RenameAccountCommand, Guid>, AuthorizedHandlerAdapter<RenameAccountCommand, Account, Guid>>();

		return services;
	}
}