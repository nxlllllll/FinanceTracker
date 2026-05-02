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
        services.AddScoped<IAuthorizedHandler<ChangeTransactionCategoryCommand, Transaction>, ChangeTransactionCategoryHandler>();
        services.AddScoped<IAuthorizedHandler<ChangeTransactionDescriptionCommand, Transaction>, ChangeTransactionDescriptionHandler>();
        services.AddScoped<IAuthorizedHandler<IncludeTransactionCommand, Transaction>, IncludeTransactionHandler>();
        services.AddScoped<IAuthorizedHandler<ExcludeTransactionCommand, Transaction>, ExcludeTransactionHandler>();

        services.AddScoped<IRequestHandler<CreateTransactionCommand, Guid>, AuthorizedHandlerAdapter<CreateTransactionCommand, Account, Guid>>();
        services.AddScoped<IRequestHandler<ChangeTransactionCategoryCommand>, AuthorizedHandlerAdapter<ChangeTransactionCategoryCommand, Transaction>>();
        services.AddScoped<IRequestHandler<ChangeTransactionDescriptionCommand>, AuthorizedHandlerAdapter<ChangeTransactionDescriptionCommand, Transaction>>();
        services.AddScoped<IRequestHandler<IncludeTransactionCommand>, AuthorizedHandlerAdapter<IncludeTransactionCommand, Transaction>>();
        services.AddScoped<IRequestHandler<ExcludeTransactionCommand>, AuthorizedHandlerAdapter<ExcludeTransactionCommand, Transaction>>();

        return services;
    }
}