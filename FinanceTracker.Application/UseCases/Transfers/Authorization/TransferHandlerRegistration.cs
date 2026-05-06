using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transfers.Commands;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application.UseCases.Transfers.Authorization;

internal static class TransferHandlerRegistration
{
	internal static IServiceCollection RegisterTransferHandlers(this IServiceCollection services)
	{
		services.AddScoped<TransferLoader>();
		services.AddScoped<IEntityLoader<CreateTransferCommand, (Account, Account), DomainException>>(
			implementationFactory: sp => sp.GetRequiredService<TransferLoader>()
		);

		services.AddScoped<IAuthorizedHandler<CreateTransferCommand, (Account, Account), Guid, DomainException>, CreateTransferHandler>();

		services.AddScoped<IRequestHandler<CreateTransferCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<CreateTransferCommand, (Account, Account), Guid, DomainException>>();

		return services;
	}
}