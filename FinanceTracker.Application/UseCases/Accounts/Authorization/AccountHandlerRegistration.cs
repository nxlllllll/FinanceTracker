using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Accounts.Commands.ArchiveAccount;
using FinanceTracker.Application.UseCases.Accounts.Commands.RenameAccount;
using FinanceTracker.Application.UseCases.Accounts.Commands.UnarchiveAccount;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application.UseCases.Accounts.Authorization;

internal static class AccountHandlerRegistration
{
	internal static IServiceCollection RegisterAccountHandlers(this IServiceCollection services)
	{
		services.AddScoped<AccountLoader>();
		services.AddScoped<IEntityLoader<ArchiveAccountCommand, Account>>(sp => sp.GetRequiredService<AccountLoader>());
		services.AddScoped<IEntityLoader<UnarchiveAccountCommand, Account>>(sp => sp.GetRequiredService<AccountLoader>());
		services.AddScoped<IEntityLoader<RenameAccountCommand, Account>>(sp => sp.GetRequiredService<AccountLoader>());

		services.AddScoped<IAuthorizedHandler<ArchiveAccountCommand, Account, Guid, DomainException>, ArchiveAccountHandler>();
		services.AddScoped<IAuthorizedHandler<UnarchiveAccountCommand, Account, Guid, DomainException>, UnarchiveAccountHandler>();
		services.AddScoped<IAuthorizedHandler<RenameAccountCommand, Account, Guid, DomainException>, RenameAccountHandler>();

		services.AddScoped<IRequestHandler<ArchiveAccountCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<ArchiveAccountCommand, Account, Guid, DomainException>>();
		services.AddScoped<IRequestHandler<UnarchiveAccountCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<UnarchiveAccountCommand, Account, Guid, DomainException>>();
		services.AddScoped<IRequestHandler<RenameAccountCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<RenameAccountCommand, Account, Guid, DomainException>>();

		return services;
	}
}