using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Transfers.Commands;
using FinanceTracker.Core.Domains.Account;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application.Transfers.Authorization;

internal static class TransferHandlerRegistration
{
	internal static IServiceCollection RegisterTransferHandlers(this IServiceCollection services)
	{
		services.AddScoped<TransferLoader>();
		services.AddScoped<IEntityLoader<CreateTransferCommand, (Account, Account)>>(sp => sp.GetRequiredService<TransferLoader>());

		services.AddScoped<IAuthorizedHandler<CreateTransferCommand, (Account, Account), Guid>, CreateTransferHandler>();

		services.AddScoped<IRequestHandler<CreateTransferCommand, Guid>, AuthorizedHandlerAdapter<CreateTransferCommand, (Account, Account), Guid>>();

		return services;
	}
}