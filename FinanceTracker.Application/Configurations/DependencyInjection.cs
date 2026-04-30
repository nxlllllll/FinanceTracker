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
using FinanceTracker.Application.Users.Authorization;
using FinanceTracker.Application.Users.Commands.ChangeUserBaseCurrency;
using FinanceTracker.Application.Users.Commands.ChangeUserEmail;
using FinanceTracker.Application.Users.Commands.ChangeUserPassword;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Domains.User;
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

#region User
		services.AddScoped<UserLoader>();
		services.AddScoped<IEntityLoader<ChangeUserBaseCurrencyCommand, User>>(
			implementationFactory: sp => sp.GetRequiredService<UserLoader>()
		);
		services.AddScoped<IEntityLoader<ChangeUserEmailCommand, User>>(
			implementationFactory: sp => sp.GetRequiredService<UserLoader>()
		);
		services.AddScoped<IEntityLoader<ChangeUserPasswordCommand, User>>(
			implementationFactory: sp => sp.GetRequiredService<UserLoader>()
		);
#endregion User

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
		services.AddScoped<IEntityLoader<ChangeTransactionCategoryCommand, Transaction>>(
			implementationFactory: sp => sp.GetRequiredService<TransactionLoader>()
		);
		services.AddScoped<IEntityLoader<ChangeTransactionDescriptionCommand, Transaction>>(
			implementationFactory: sp => sp.GetRequiredService<TransactionLoader>()
		);
		services.AddScoped<IEntityLoader<IncludeTransactionCommand, Transaction>>(
			implementationFactory: sp => sp.GetRequiredService<TransactionLoader>()
		);
		services.AddScoped<IEntityLoader<ExcludeTransactionCommand, Transaction>>(
			implementationFactory: sp => sp.GetRequiredService<TransactionLoader>()
		);
#endregion Transaction

#region RecurringTransaction
		services.AddScoped<RecurringTransactionLoader>();
		services.AddScoped<IEntityLoader<ActivateRecurringTransactionCommand, RecurringTransaction>>(
			implementationFactory: sp => sp.GetRequiredService<RecurringTransactionLoader>()
		);
		services.AddScoped<IEntityLoader<DeactivateRecurringTransactionCommand, RecurringTransaction>>(
			implementationFactory: sp => sp.GetRequiredService<RecurringTransactionLoader>()
		);
		services.AddScoped<IEntityLoader<ChangeRecurringTransactionAmountCommand, RecurringTransaction>>(
			implementationFactory: sp => sp.GetRequiredService<RecurringTransactionLoader>()
		);
		services.AddScoped<IEntityLoader<ChangeRecurringTransactionCurrencyCommand, RecurringTransaction>>(
			implementationFactory: sp => sp.GetRequiredService<RecurringTransactionLoader>()
		);
		services.AddScoped<IEntityLoader<ChangeRecurringTransactionDayOfMonthCommand, RecurringTransaction>>(
			implementationFactory: sp => sp.GetRequiredService<RecurringTransactionLoader>()
		);
#endregion RecurringTransaction

#region Budget
		services.AddScoped<BudgetLoader>();
		services.AddScoped<IEntityLoader<ChangeBudgetAmountCommand, Budget>>(
			implementationFactory: sp => sp.GetRequiredService<BudgetLoader>()
		);
		services.AddScoped<IEntityLoader<ChangeBudgetPeriodCommand, Budget>>(
			implementationFactory: sp => sp.GetRequiredService<BudgetLoader>()
		);
		services.AddScoped<IEntityLoader<DeleteBudgetCommand, Budget>>(
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

#region User
		services.AddScoped<IAuthorizedHandler<ChangeUserBaseCurrencyCommand, User>, ChangeUserBaseCurrencyHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeUserEmailCommand, User>, ChangeUserEmailHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeUserPasswordCommand, User>, ChangeUserPasswordHandler>();

		services.AddScoped<IRequestHandler<ChangeUserBaseCurrencyCommand>, AuthorizedHandlerAdapter<ChangeUserBaseCurrencyCommand, User>>();
		services.AddScoped<IRequestHandler<ChangeUserEmailCommand>, AuthorizedHandlerAdapter<ChangeUserEmailCommand, User>>();
		services.AddScoped<IRequestHandler<ChangeUserPasswordCommand>, AuthorizedHandlerAdapter<ChangeUserPasswordCommand, User>>();
#endregion User

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
		services.AddScoped<IAuthorizedHandler<ChangeTransactionCategoryCommand, Transaction>, ChangeTransactionCategoryHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeTransactionDescriptionCommand, Transaction>, ChangeTransactionDescriptionHandler>();
		services.AddScoped<IAuthorizedHandler<IncludeTransactionCommand, Transaction>, IncludeTransactionHandler>();
		services.AddScoped<IAuthorizedHandler<ExcludeTransactionCommand, Transaction>, ExcludeTransactionHandler>();
		
		services.AddScoped<IRequestHandler<CreateTransactionCommand, Guid>, AuthorizedHandlerAdapter<CreateTransactionCommand, Account, Guid>>();
		services.AddScoped<IRequestHandler<ChangeTransactionCategoryCommand>, AuthorizedHandlerAdapter<ChangeTransactionCategoryCommand, Transaction>>();
		services.AddScoped<IRequestHandler<ChangeTransactionDescriptionCommand>, AuthorizedHandlerAdapter<ChangeTransactionDescriptionCommand, Transaction>>();
		services.AddScoped<IRequestHandler<IncludeTransactionCommand>, AuthorizedHandlerAdapter<IncludeTransactionCommand, Transaction>>();
		services.AddScoped<IRequestHandler<ExcludeTransactionCommand>, AuthorizedHandlerAdapter<ExcludeTransactionCommand, Transaction>>();
#endregion Transaction
		
#region RecurringTransaction
		services.AddScoped<IAuthorizedHandler<ActivateRecurringTransactionCommand, RecurringTransaction>, ActivateRecurringTransactionHandler>();
		services.AddScoped<IAuthorizedHandler<DeactivateRecurringTransactionCommand, RecurringTransaction>, DeactivateRecurringTransactionHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeRecurringTransactionAmountCommand, RecurringTransaction>, ChangeRecurringTransactionAmountHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeRecurringTransactionCurrencyCommand, RecurringTransaction>, ChangeRecurringTransactionCurrencyHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeRecurringTransactionDayOfMonthCommand, RecurringTransaction>, ChangeRecurringTransactionDayOfMonthHandler>();
		
		services.AddScoped<IRequestHandler<ChangeRecurringTransactionDayOfMonthCommand>, AuthorizedHandlerAdapter<ChangeRecurringTransactionDayOfMonthCommand, RecurringTransaction>>();
		services.AddScoped<IRequestHandler<ActivateRecurringTransactionCommand>, AuthorizedHandlerAdapter<ActivateRecurringTransactionCommand, RecurringTransaction>>();
		services.AddScoped<IRequestHandler<DeactivateRecurringTransactionCommand>, AuthorizedHandlerAdapter<DeactivateRecurringTransactionCommand, RecurringTransaction>>();
		services.AddScoped<IRequestHandler<ChangeRecurringTransactionAmountCommand>, AuthorizedHandlerAdapter<ChangeRecurringTransactionAmountCommand, RecurringTransaction>>();
		services.AddScoped<IRequestHandler<ChangeRecurringTransactionCurrencyCommand>, AuthorizedHandlerAdapter<ChangeRecurringTransactionCurrencyCommand, RecurringTransaction>>();
#endregion RecurringTransaction
		
#region Budget
		services.AddScoped<IAuthorizedHandler<ChangeBudgetAmountCommand, Budget>, ChangeBudgetAmountHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeBudgetPeriodCommand, Budget>, ChangeBudgetPeriodHandler>();
		services.AddScoped<IAuthorizedHandler<DeleteBudgetCommand, Budget>, DeleteBudgetHandler>();
		
		services.AddScoped<IRequestHandler<ChangeBudgetPeriodCommand>, AuthorizedHandlerAdapter<ChangeBudgetPeriodCommand, Budget>>();
		services.AddScoped<IRequestHandler<DeleteBudgetCommand>, AuthorizedHandlerAdapter<DeleteBudgetCommand, Budget>>();
		services.AddScoped<IRequestHandler<ChangeBudgetAmountCommand>, AuthorizedHandlerAdapter<ChangeBudgetAmountCommand, Budget>>();
#endregion Budget
		
#region Transfer
		services.AddScoped<IAuthorizedHandler<CreateTransferCommand, (Account, Account), Guid>, CreateTransferHandler>();
		services.AddScoped<IRequestHandler<CreateTransferCommand, Guid>, AuthorizedHandlerAdapter<CreateTransferCommand, (Account, Account), Guid>>();
#endregion Transfer
		
#endregion Handler + Adapter
	}
}