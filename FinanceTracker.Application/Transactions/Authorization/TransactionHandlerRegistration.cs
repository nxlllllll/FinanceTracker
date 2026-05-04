using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Transactions.Commands.ChangeTransactionCategory;
using FinanceTracker.Application.Transactions.Commands.ChangeTransactionDescription;
using FinanceTracker.Application.Transactions.Commands.CreateTransaction;
using FinanceTracker.Application.Transactions.Commands.ExcludeTransaction;
using FinanceTracker.Application.Transactions.Commands.IncludeTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transaction;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application.Transactions.Authorization;

internal static class TransactionHandlerRegistration
{
    internal static IServiceCollection RegisterTransactionHandlers(this IServiceCollection services)
    {
        services.AddScoped<TransactionLoader>();
        services.AddScoped<IEntityLoader<CreateTransactionCommand, Account>>(sp => sp.GetRequiredService<TransactionLoader>());
        services.AddScoped<IEntityLoader<ChangeTransactionCategoryCommand, Transaction>>(sp => sp.GetRequiredService<TransactionLoader>());
        services.AddScoped<IEntityLoader<ChangeTransactionDescriptionCommand, Transaction>>(sp => sp.GetRequiredService<TransactionLoader>());
        services.AddScoped<IEntityLoader<IncludeTransactionCommand, Transaction>>(sp => sp.GetRequiredService<TransactionLoader>());
        services.AddScoped<IEntityLoader<ExcludeTransactionCommand, Transaction>>(sp => sp.GetRequiredService<TransactionLoader>());

        services.AddScoped<IAuthorizedHandler<CreateTransactionCommand, Account, Guid>, CreateTransactionHandler>();
        services.AddScoped<IAuthorizedHandler<ChangeTransactionCategoryCommand, Transaction, Guid>, ChangeTransactionCategoryHandler>();
        services.AddScoped<IAuthorizedHandler<ChangeTransactionDescriptionCommand, Transaction, Guid>, ChangeTransactionDescriptionHandler>();
        services.AddScoped<IAuthorizedHandler<IncludeTransactionCommand, Transaction, Guid>, IncludeTransactionHandler>();
        services.AddScoped<IAuthorizedHandler<ExcludeTransactionCommand, Transaction, Guid>, ExcludeTransactionHandler>();

        services.AddScoped<IRequestHandler<CreateTransactionCommand, Guid>, AuthorizedHandlerAdapter<CreateTransactionCommand, Account, Guid>>();
        services.AddScoped<IRequestHandler<ChangeTransactionCategoryCommand, Guid>, AuthorizedHandlerAdapter<ChangeTransactionCategoryCommand, Transaction, Guid>>();
        services.AddScoped<IRequestHandler<ChangeTransactionDescriptionCommand, Guid>, AuthorizedHandlerAdapter<ChangeTransactionDescriptionCommand, Transaction, Guid>>();
        services.AddScoped<IRequestHandler<IncludeTransactionCommand, Guid>, AuthorizedHandlerAdapter<IncludeTransactionCommand, Transaction, Guid>>();
        services.AddScoped<IRequestHandler<ExcludeTransactionCommand, Guid>, AuthorizedHandlerAdapter<ExcludeTransactionCommand, Transaction, Guid>>();

        return services;
    }
}