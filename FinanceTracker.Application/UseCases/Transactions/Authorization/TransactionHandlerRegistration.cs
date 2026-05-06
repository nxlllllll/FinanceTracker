using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transactions.Commands.ChangeTransactionCategory;
using FinanceTracker.Application.UseCases.Transactions.Commands.ChangeTransactionDescription;
using FinanceTracker.Application.UseCases.Transactions.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transactions.Commands.ExcludeTransaction;
using FinanceTracker.Application.UseCases.Transactions.Commands.IncludeTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application.UseCases.Transactions.Authorization;

internal static class TransactionHandlerRegistration
{
    internal static IServiceCollection RegisterTransactionHandlers(this IServiceCollection services)
    {
        services.AddScoped<TransactionLoader>();
        services.AddScoped<IEntityLoader<CreateTransactionCommand, Account, DomainException>>(
            implementationFactory: sp => sp.GetRequiredService<TransactionLoader>()
        );
        services.AddScoped<IEntityLoader<ChangeTransactionCategoryCommand, Transaction, DomainException>>(
            implementationFactory: sp => sp.GetRequiredService<TransactionLoader>()
        );
        services.AddScoped<IEntityLoader<ChangeTransactionDescriptionCommand, Transaction, DomainException>>(
            implementationFactory: sp => sp.GetRequiredService<TransactionLoader>()
        );
        services.AddScoped<IEntityLoader<IncludeTransactionCommand, Transaction, DomainException>>(
            implementationFactory: sp => sp.GetRequiredService<TransactionLoader>()
        );
        services.AddScoped<IEntityLoader<ExcludeTransactionCommand, Transaction, DomainException>>(
            implementationFactory: sp => sp.GetRequiredService<TransactionLoader>()
        );

        services.AddScoped<IAuthorizedHandler<CreateTransactionCommand, Account, Guid, DomainException>, CreateTransactionHandler>();
        services.AddScoped<IAuthorizedHandler<ChangeTransactionCategoryCommand, Transaction, Guid, DomainException>, ChangeTransactionCategoryHandler>();
        services.AddScoped<IAuthorizedHandler<ChangeTransactionDescriptionCommand, Transaction, Guid, DomainException>, ChangeTransactionDescriptionHandler>();
        services.AddScoped<IAuthorizedHandler<IncludeTransactionCommand, Transaction, Guid, DomainException>, IncludeTransactionHandler>();
        services.AddScoped<IAuthorizedHandler<ExcludeTransactionCommand, Transaction, Guid, DomainException>, ExcludeTransactionHandler>();

        services.AddScoped<IRequestHandler<CreateTransactionCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<CreateTransactionCommand, Account, Guid, DomainException>>();
        services.AddScoped<IRequestHandler<ChangeTransactionCategoryCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<ChangeTransactionCategoryCommand, Transaction, Guid, DomainException>>();
        services.AddScoped<IRequestHandler<ChangeTransactionDescriptionCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<ChangeTransactionDescriptionCommand, Transaction, Guid, DomainException>>();
        services.AddScoped<IRequestHandler<IncludeTransactionCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<IncludeTransactionCommand, Transaction, Guid, DomainException>>();
        services.AddScoped<IRequestHandler<ExcludeTransactionCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<ExcludeTransactionCommand, Transaction, Guid, DomainException>>();

        return services;
    }
}