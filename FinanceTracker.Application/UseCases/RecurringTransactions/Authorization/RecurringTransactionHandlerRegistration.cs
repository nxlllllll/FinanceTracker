using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ActivateRecurringTransaction;
using FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ChangeRecurringTransactionAmount;
using FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ChangeRecurringTransactionCurrency;
using FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ChangeRecurringTransactionDayOfMonth;
using FinanceTracker.Application.UseCases.RecurringTransactions.Commands.DeactivateRecurringTransaction;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Authorization;

internal static class RecurringTransactionHandlerRegistration
{
    internal static IServiceCollection RegisterRecurringTransactionHandlers(this IServiceCollection services)
    {
        services.AddScoped<RecurringTransactionLoader>();
        services.AddScoped<IEntityLoader<ActivateRecurringTransactionCommand, RecurringTransaction, NotFoundException>>(
            implementationFactory: sp => sp.GetRequiredService<RecurringTransactionLoader>()
        );
        services.AddScoped<IEntityLoader<DeactivateRecurringTransactionCommand, RecurringTransaction, NotFoundException>>(
            implementationFactory: sp => sp.GetRequiredService<RecurringTransactionLoader>()
        );
        services.AddScoped<IEntityLoader<ChangeRecurringTransactionAmountCommand, RecurringTransaction, NotFoundException>>(
            implementationFactory: sp => sp.GetRequiredService<RecurringTransactionLoader>()
        );
        services.AddScoped<IEntityLoader<ChangeRecurringTransactionCurrencyCommand, RecurringTransaction, NotFoundException>>(
            implementationFactory: sp => sp.GetRequiredService<RecurringTransactionLoader>()
        );
        services.AddScoped<IEntityLoader<ChangeRecurringTransactionDayOfMonthCommand, RecurringTransaction, NotFoundException>>(
            implementationFactory: sp => sp.GetRequiredService<RecurringTransactionLoader>()
        );

        services.AddScoped<IAuthorizedHandler<ActivateRecurringTransactionCommand, RecurringTransaction, Guid, DomainException>, ActivateRecurringTransactionHandler>();
        services.AddScoped<IAuthorizedHandler<DeactivateRecurringTransactionCommand, RecurringTransaction, Guid, DomainException>, DeactivateRecurringTransactionHandler>();
        services.AddScoped<IAuthorizedHandler<ChangeRecurringTransactionAmountCommand, RecurringTransaction, Guid, DomainException>, ChangeRecurringTransactionAmountHandler>();
        services.AddScoped<IAuthorizedHandler<ChangeRecurringTransactionCurrencyCommand, RecurringTransaction, Guid, DomainException>, ChangeRecurringTransactionCurrencyHandler>();
        services.AddScoped<IAuthorizedHandler<ChangeRecurringTransactionDayOfMonthCommand, RecurringTransaction, Guid, DomainException>, ChangeRecurringTransactionDayOfMonthHandler>();

        services.AddScoped<IRequestHandler<ActivateRecurringTransactionCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<ActivateRecurringTransactionCommand, RecurringTransaction, Guid, DomainException>>();
        services.AddScoped<IRequestHandler<DeactivateRecurringTransactionCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<DeactivateRecurringTransactionCommand, RecurringTransaction, Guid, DomainException>>();
        services.AddScoped<IRequestHandler<ChangeRecurringTransactionAmountCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<ChangeRecurringTransactionAmountCommand, RecurringTransaction, Guid, DomainException>>();
        services.AddScoped<IRequestHandler<ChangeRecurringTransactionCurrencyCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<ChangeRecurringTransactionCurrencyCommand, RecurringTransaction, Guid, DomainException>>();
        services.AddScoped<IRequestHandler<ChangeRecurringTransactionDayOfMonthCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<ChangeRecurringTransactionDayOfMonthCommand, RecurringTransaction, Guid, DomainException>>();

        return services;
    }
}