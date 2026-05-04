using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.RecurringTransactions.Commands.ActivateRecurringTransaction;
using FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionAmount;
using FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionCurrency;
using FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionDayOfMonth;
using FinanceTracker.Application.RecurringTransactions.Commands.DeactivateRecurringTransaction;
using FinanceTracker.Core.Domains.RecurringTransaction;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application.RecurringTransactions.Authorization;

internal static class RecurringTransactionHandlerRegistration
{
    internal static IServiceCollection RegisterRecurringTransactionHandlers(this IServiceCollection services)
    {
        services.AddScoped<RecurringTransactionLoader>();
        services.AddScoped<IEntityLoader<ActivateRecurringTransactionCommand, RecurringTransaction>>(sp => sp.GetRequiredService<RecurringTransactionLoader>());
        services.AddScoped<IEntityLoader<DeactivateRecurringTransactionCommand, RecurringTransaction>>(sp => sp.GetRequiredService<RecurringTransactionLoader>());
        services.AddScoped<IEntityLoader<ChangeRecurringTransactionAmountCommand, RecurringTransaction>>(sp => sp.GetRequiredService<RecurringTransactionLoader>());
        services.AddScoped<IEntityLoader<ChangeRecurringTransactionCurrencyCommand, RecurringTransaction>>(sp => sp.GetRequiredService<RecurringTransactionLoader>());
        services.AddScoped<IEntityLoader<ChangeRecurringTransactionDayOfMonthCommand, RecurringTransaction>>(sp => sp.GetRequiredService<RecurringTransactionLoader>());

        services.AddScoped<IAuthorizedHandler<ActivateRecurringTransactionCommand, RecurringTransaction, Guid>, ActivateRecurringTransactionHandler>();
        services.AddScoped<IAuthorizedHandler<DeactivateRecurringTransactionCommand, RecurringTransaction, Guid>, DeactivateRecurringTransactionHandler>();
        services.AddScoped<IAuthorizedHandler<ChangeRecurringTransactionAmountCommand, RecurringTransaction, Guid>, ChangeRecurringTransactionAmountHandler>();
        services.AddScoped<IAuthorizedHandler<ChangeRecurringTransactionCurrencyCommand, RecurringTransaction, Guid>, ChangeRecurringTransactionCurrencyHandler>();
        services.AddScoped<IAuthorizedHandler<ChangeRecurringTransactionDayOfMonthCommand, RecurringTransaction, Guid>, ChangeRecurringTransactionDayOfMonthHandler>();

        services.AddScoped<IRequestHandler<ActivateRecurringTransactionCommand, Guid>, AuthorizedHandlerAdapter<ActivateRecurringTransactionCommand, RecurringTransaction, Guid>>();
        services.AddScoped<IRequestHandler<DeactivateRecurringTransactionCommand, Guid>, AuthorizedHandlerAdapter<DeactivateRecurringTransactionCommand, RecurringTransaction, Guid>>();
        services.AddScoped<IRequestHandler<ChangeRecurringTransactionAmountCommand, Guid>, AuthorizedHandlerAdapter<ChangeRecurringTransactionAmountCommand, RecurringTransaction, Guid>>();
        services.AddScoped<IRequestHandler<ChangeRecurringTransactionCurrencyCommand, Guid>, AuthorizedHandlerAdapter<ChangeRecurringTransactionCurrencyCommand, RecurringTransaction, Guid>>();
        services.AddScoped<IRequestHandler<ChangeRecurringTransactionDayOfMonthCommand, Guid>, AuthorizedHandlerAdapter<ChangeRecurringTransactionDayOfMonthCommand, RecurringTransaction, Guid>>();

        return services;
    }
}