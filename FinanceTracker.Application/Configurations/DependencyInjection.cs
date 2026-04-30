using FinanceTracker.Application.Accounts.Authorization;
using FinanceTracker.Application.Accounts.Commands.ArchiveAccount;
using FinanceTracker.Application.Accounts.Commands.RenameAccount;
using FinanceTracker.Application.Accounts.Commands.UnarchiveAccount;
using FinanceTracker.Application.Accounts.Notifications;
using FinanceTracker.Application.Behaviours;
using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Budgets.Authorization;
using FinanceTracker.Application.Budgets.Commands.ChangeBudgetAmount;
using FinanceTracker.Application.Budgets.Commands.ChangeBudgetPeriod;
using FinanceTracker.Application.Budgets.Commands.DeleteBudget;
using FinanceTracker.Application.Categories.Authorization;
using FinanceTracker.Application.Categories.Commands.ArchiveCategory;
using FinanceTracker.Application.Categories.Commands.RenameCategory;
using FinanceTracker.Application.Categories.Commands.UnarchiveCategory;
using FinanceTracker.Application.Dispatching;
using FinanceTracker.Application.RecurringTransactions.Authorization;
using FinanceTracker.Application.RecurringTransactions.Commands.ActivateRecurringTransaction;
using FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionAmount;
using FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionCurrency;
using FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionDayOfMonth;
using FinanceTracker.Application.RecurringTransactions.Commands.DeactivateRecurringTransaction;
using FinanceTracker.Application.Transactions.Authorization;
using FinanceTracker.Application.Transactions.Commands.ChangeTransactionCategory;
using FinanceTracker.Application.Transactions.Commands.ChangeTransactionDescription;
using FinanceTracker.Application.Transactions.Commands.CreateTransaction;
using FinanceTracker.Application.Transactions.Commands.ExcludeTransaction;
using FinanceTracker.Application.Transactions.Commands.IncludeTransaction;
using FinanceTracker.Application.Transfers.Authorization;
using FinanceTracker.Application.Transfers.Commands;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Dtos;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application.Configurations;

public static class DependencyInjection
{
	public static IServiceCollection AddApplication(this IServiceCollection services)
	{
		services.AddMediatR(configuration: configuration =>
		{
			configuration.RegisterServicesFromAssembly(assembly: typeof(DependencyInjection).Assembly);
			configuration.AddBehavior(serviceType: typeof(IPipelineBehavior<,>),
				implementationType: typeof(ValidationBehavior<,>));
		});

		services.AddValidatorsFromAssembly(assembly: typeof(DependencyInjection).Assembly);
		services.AddScoped<INotificationDispatcher, MediatRNotificationDispatcher>();

		services.AddSingleton<IAggregateNotificationFactory, AccountNotificationFactory>();

		services.RegisterAuthorizedHandlers();
		
		return services;
	}

	private static void RegisterAuthorizedHandlers(this IServiceCollection services)
	{
#region Loaders
#region Account
		services.AddScoped<AccountLoader>();
		services.AddScoped<IEntityLoader<ArchiveAccountCommand, Account>>(
			implementationFactory: sp => sp.GetRequiredService<AccountLoader>()
		);
		services.AddScoped<IEntityLoader<UnarchiveAccountCommand, Account>>(
			implementationFactory: sp => sp.GetRequiredService<AccountLoader>()
		);
		services.AddScoped<IEntityLoader<RenameAccountCommand, Account>>(
			implementationFactory: sp => sp.GetRequiredService<AccountLoader>()
		);
#endregion Account

#region Category
		services.AddScoped<CategoryLoader>();
		services.AddScoped<IEntityLoader<ArchiveCategoryCommand, Category>>(
			implementationFactory: sp => sp.GetRequiredService<CategoryLoader>()
		);
		services.AddScoped<IEntityLoader<UnarchiveCategoryCommand, Category>>(
			implementationFactory: sp => sp.GetRequiredService<CategoryLoader>()
		);
		services.AddScoped<IEntityLoader<RenameCategoryCommand, Category>>(
			implementationFactory: sp => sp.GetRequiredService<CategoryLoader>()
		);
#endregion Category
		
#region Transaction
		services.AddScoped<TransactionLoader>();
		services.AddScoped<IEntityLoader<ChangeTransactionCategoryCommand, TransactionDto>>(
			implementationFactory: sp => sp.GetRequiredService<TransactionLoader>()
		);
		services.AddScoped<IEntityLoader<ChangeTransactionDescriptionCommand, TransactionDto>>(
			implementationFactory: sp => sp.GetRequiredService<TransactionLoader>()
		);
		services.AddScoped<IEntityLoader<IncludeTransactionCommand, TransactionDto>>(
			implementationFactory: sp => sp.GetRequiredService<TransactionLoader>()
		);
		services.AddScoped<IEntityLoader<ExcludeTransactionCommand, TransactionDto>>(
			implementationFactory: sp => sp.GetRequiredService<TransactionLoader>()
		);
#endregion Transaction

#region RecurringTransaction
		services.AddScoped<RecurringTransactionLoader>();
		services.AddScoped<IEntityLoader<ActivateRecurringTransactionCommand, RecurringTransactionDto>>(
			implementationFactory: sp => sp.GetRequiredService<RecurringTransactionLoader>()
		);
		services.AddScoped<IEntityLoader<DeactivateRecurringTransactionCommand, RecurringTransactionDto>>(
			implementationFactory: sp => sp.GetRequiredService<RecurringTransactionLoader>()
		);
		services.AddScoped<IEntityLoader<ChangeRecurringTransactionAmountCommand, RecurringTransactionDto>>(
			implementationFactory: sp => sp.GetRequiredService<RecurringTransactionLoader>()
		);
		services.AddScoped<IEntityLoader<ChangeRecurringTransactionCurrencyCommand, RecurringTransactionDto>>(
			implementationFactory: sp => sp.GetRequiredService<RecurringTransactionLoader>()
		);
		services.AddScoped<IEntityLoader<ChangeRecurringTransactionDayOfMonthCommand, RecurringTransactionDto>>(
			implementationFactory: sp => sp.GetRequiredService<RecurringTransactionLoader>()
		);
#endregion RecurringTransaction

#region Budget
		services.AddScoped<BudgetLoader>();
		services.AddScoped<IEntityLoader<ChangeBudgetAmountCommand, BudgetDto>>(
			implementationFactory: sp => sp.GetRequiredService<BudgetLoader>()
		);
		services.AddScoped<IEntityLoader<ChangeBudgetPeriodCommand, BudgetDto>>(
			implementationFactory: sp => sp.GetRequiredService<BudgetLoader>()
		);
		services.AddScoped<IEntityLoader<DeleteBudgetCommand, BudgetDto>>(
			implementationFactory: sp => sp.GetRequiredService<BudgetLoader>()
		);
#endregion Budget

#region Transfer
		services.AddScoped<TransferLoader>();
		services.AddScoped<IEntityLoader<CreateTransferCommand, (Account, Account)>>(
			implementationFactory: sp => sp.GetRequiredService<TransferLoader>()
		);
#endregion Transfer
#endregion Loaders

#region Handler + Adapter
#region Account
		services.AddScoped<IAuthorizedHandler<ArchiveAccountCommand, Account>, ArchiveAccountHandler>();
		services.AddScoped<IAuthorizedHandler<UnarchiveAccountCommand, Account>, UnarchiveAccountHandler>();
		services.AddScoped<IAuthorizedHandler<RenameAccountCommand, Account>, RenameAccountHandler>();

		services.AddScoped<IRequestHandler<ArchiveAccountCommand>, AuthorizedHandlerAdapter<ArchiveAccountCommand, Account>>();
		services.AddScoped<IRequestHandler<UnarchiveAccountCommand>, AuthorizedHandlerAdapter<UnarchiveAccountCommand, Account>>();
		services.AddScoped<IRequestHandler<RenameAccountCommand>, AuthorizedHandlerAdapter<RenameAccountCommand, Account>>();
#endregion Account

#region Category
		services.AddScoped<IAuthorizedHandler<ArchiveCategoryCommand, Category>, ArchiveCategoryHandler>();
		services.AddScoped<IAuthorizedHandler<UnarchiveCategoryCommand, Category>, UnarchiveCategoryHandler>();
		services.AddScoped<IAuthorizedHandler<RenameCategoryCommand, Category>, RenameCategoryHandler>();
		
		services.AddScoped<IRequestHandler<ArchiveCategoryCommand>, AuthorizedHandlerAdapter<ArchiveCategoryCommand, Category>>();
		services.AddScoped<IRequestHandler<UnarchiveCategoryCommand>, AuthorizedHandlerAdapter<UnarchiveCategoryCommand, Category>>();
		services.AddScoped<IRequestHandler<RenameCategoryCommand>, AuthorizedHandlerAdapter<RenameCategoryCommand, Category>>();
#endregion Category
		
#region Transaction
		services.AddScoped<IAuthorizedHandler<CreateTransactionCommand, Account, Guid>, CreateTransactionHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeTransactionCategoryCommand, TransactionDto>, ChangeTransactionCategoryHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeTransactionDescriptionCommand, TransactionDto>, ChangeTransactionDescriptionHandler>();
		services.AddScoped<IAuthorizedHandler<IncludeTransactionCommand, TransactionDto>, IncludeTransactionHandler>();
		services.AddScoped<IAuthorizedHandler<ExcludeTransactionCommand, TransactionDto>, ExcludeTransactionHandler>();
		
		services.AddScoped<IRequestHandler<CreateTransactionCommand, Guid>, AuthorizedHandlerAdapter<CreateTransactionCommand, Account, Guid>>();
		services.AddScoped<IRequestHandler<ChangeTransactionCategoryCommand>, AuthorizedHandlerAdapter<ChangeTransactionCategoryCommand, TransactionDto>>();
		services.AddScoped<IRequestHandler<ChangeTransactionDescriptionCommand>, AuthorizedHandlerAdapter<ChangeTransactionDescriptionCommand, TransactionDto>>();
		services.AddScoped<IRequestHandler<IncludeTransactionCommand>, AuthorizedHandlerAdapter<IncludeTransactionCommand, TransactionDto>>();
		services.AddScoped<IRequestHandler<ExcludeTransactionCommand>, AuthorizedHandlerAdapter<ExcludeTransactionCommand, TransactionDto>>();
#endregion Transaction
		
#region RecurringTransaction
		services.AddScoped<IAuthorizedHandler<ActivateRecurringTransactionCommand, RecurringTransactionDto>, ActivateRecurringTransactionHandler>();
		services.AddScoped<IAuthorizedHandler<DeactivateRecurringTransactionCommand, RecurringTransactionDto>, DeactivateRecurringTransactionHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeRecurringTransactionAmountCommand, RecurringTransactionDto>, ChangeRecurringTransactionAmountHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeRecurringTransactionCurrencyCommand, RecurringTransactionDto>, ChangeRecurringTransactionCurrencyHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeRecurringTransactionDayOfMonthCommand, RecurringTransactionDto>, ChangeRecurringTransactionDayOfMonthHandler>();
		
		services.AddScoped<IRequestHandler<ChangeRecurringTransactionDayOfMonthCommand>, AuthorizedHandlerAdapter<ChangeRecurringTransactionDayOfMonthCommand, RecurringTransactionDto>>();
		services.AddScoped<IRequestHandler<ActivateRecurringTransactionCommand>, AuthorizedHandlerAdapter<ActivateRecurringTransactionCommand, RecurringTransactionDto>>();
		services.AddScoped<IRequestHandler<DeactivateRecurringTransactionCommand>, AuthorizedHandlerAdapter<DeactivateRecurringTransactionCommand, RecurringTransactionDto>>();
		services.AddScoped<IRequestHandler<ChangeRecurringTransactionAmountCommand>, AuthorizedHandlerAdapter<ChangeRecurringTransactionAmountCommand, RecurringTransactionDto>>();
		services.AddScoped<IRequestHandler<ChangeRecurringTransactionCurrencyCommand>, AuthorizedHandlerAdapter<ChangeRecurringTransactionCurrencyCommand, RecurringTransactionDto>>();
#endregion RecurringTransaction
		
#region Budget
		services.AddScoped<IAuthorizedHandler<ChangeBudgetAmountCommand, BudgetDto>, ChangeBudgetAmountHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeBudgetPeriodCommand, BudgetDto>, ChangeBudgetPeriodHandler>();
		services.AddScoped<IAuthorizedHandler<DeleteBudgetCommand, BudgetDto>, DeleteBudgetHandler>();
		
		services.AddScoped<IRequestHandler<ChangeBudgetPeriodCommand>, AuthorizedHandlerAdapter<ChangeBudgetPeriodCommand, BudgetDto>>();
		services.AddScoped<IRequestHandler<DeleteBudgetCommand>, AuthorizedHandlerAdapter<DeleteBudgetCommand, BudgetDto>>();
		services.AddScoped<IRequestHandler<ChangeBudgetAmountCommand>, AuthorizedHandlerAdapter<ChangeBudgetAmountCommand, BudgetDto>>();
#endregion Budget
		
#region Transfer
		services.AddScoped<IAuthorizedHandler<CreateTransferCommand, (Account, Account), Guid>, CreateTransferHandler>();
		services.AddScoped<IRequestHandler<CreateTransferCommand, Guid>, AuthorizedHandlerAdapter<CreateTransferCommand, (Account, Account), Guid>>();
#endregion Transfer
		
#endregion Handler + Adapter
	}
}