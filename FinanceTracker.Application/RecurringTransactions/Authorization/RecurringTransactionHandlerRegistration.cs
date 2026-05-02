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

        services.AddScoped<IAuthorizedHandler<ActivateRecurringTransactionCommand, RecurringTransaction>, ActivateRecurringTransactionHandler>();
        services.AddScoped<IAuthorizedHandler<DeactivateRecurringTransactionCommand, RecurringTransaction>, DeactivateRecurringTransactionHandler>();
        services.AddScoped<IAuthorizedHandler<ChangeRecurringTransactionAmountCommand, RecurringTransaction>, ChangeRecurringTransactionAmountHandler>();
        services.AddScoped<IAuthorizedHandler<ChangeRecurringTransactionCurrencyCommand, RecurringTransaction>, ChangeRecurringTransactionCurrencyHandler>();
        services.AddScoped<IAuthorizedHandler<ChangeRecurringTransactionDayOfMonthCommand, RecurringTransaction>, ChangeRecurringTransactionDayOfMonthHandler>();

        services.AddScoped<IRequestHandler<ActivateRecurringTransactionCommand>, AuthorizedHandlerAdapter<ActivateRecurringTransactionCommand, RecurringTransaction>>();
        services.AddScoped<IRequestHandler<DeactivateRecurringTransactionCommand>, AuthorizedHandlerAdapter<DeactivateRecurringTransactionCommand, RecurringTransaction>>();
        services.AddScoped<IRequestHandler<ChangeRecurringTransactionAmountCommand>, AuthorizedHandlerAdapter<ChangeRecurringTransactionAmountCommand, RecurringTransaction>>();
        services.AddScoped<IRequestHandler<ChangeRecurringTransactionCurrencyCommand>, AuthorizedHandlerAdapter<ChangeRecurringTransactionCurrencyCommand, RecurringTransaction>>();
        services.AddScoped<IRequestHandler<ChangeRecurringTransactionDayOfMonthCommand>, AuthorizedHandlerAdapter<ChangeRecurringTransactionDayOfMonthCommand, RecurringTransaction>>();

        return services;
    }
}