using FinanceTracker.Infrastructure.Database.Context.Account;
using FinanceTracker.Infrastructure.Database.Context.Budget;
using FinanceTracker.Infrastructure.Database.Context.Category;
using FinanceTracker.Infrastructure.Database.Context.Currency;
using FinanceTracker.Infrastructure.Database.Context.EventStore;
using FinanceTracker.Infrastructure.Database.Context.Idempotency;
using FinanceTracker.Infrastructure.Database.Context.Operation;
using FinanceTracker.Infrastructure.Database.Context.Outbox;
using FinanceTracker.Infrastructure.Database.Context.ProcessedMessage;
using FinanceTracker.Infrastructure.Database.Context.RecurringTransaction;
using FinanceTracker.Infrastructure.Database.Context.Role;
using FinanceTracker.Infrastructure.Database.Context.Transaction;
using FinanceTracker.Infrastructure.Database.Context.Transfer;
using FinanceTracker.Infrastructure.Database.Context.UnresolvableEvent;
using FinanceTracker.Infrastructure.Database.Context.User;
using FinanceTracker.Infrastructure.Database.Context.UserPermission;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Context;

public sealed class FinanceTrackerContext(DbContextOptions<FinanceTrackerContext> options) : DbContext(options)
{
	public DbSet<AccountBalanceAppliedEventEntity> AccountBalanceAppliedEvents => Set<AccountBalanceAppliedEventEntity>();

	public DbSet<AccountBalanceEntity> AccountBalances => Set<AccountBalanceEntity>();

	public DbSet<AccountEntity> Accounts => Set<AccountEntity>();

	public DbSet<BaseCurrencyRecalculationEntity> BaseCurrencyRecalculations => Set<BaseCurrencyRecalculationEntity>();

	public DbSet<BudgetEntity> Budgets => Set<BudgetEntity>();

	public DbSet<BudgetProgressEntity> BudgetProgresses => Set<BudgetProgressEntity>();

	public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();

	public DbSet<CategoryTotalEntity> CategoryTotals => Set<CategoryTotalEntity>();

	public DbSet<CurrencyEntity> Currencies => Set<CurrencyEntity>();

	public DbSet<CurrencyRateEntity> CurrencyRates => Set<CurrencyRateEntity>();

	public DbSet<EventEntity> Events => Set<EventEntity>();

	public DbSet<IdempotentCommandEntity> IdempotentCommands => Set<IdempotentCommandEntity>();

	public DbSet<OperationEntity> Operations => Set<OperationEntity>();

	public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

	public DbSet<ProcessedMessageEntity> ProcessedMessages => Set<ProcessedMessageEntity>();

	public DbSet<RecurringTransactionEntity> RecurringTransactions => Set<RecurringTransactionEntity>();

	public DbSet<RoleEntity> Roles => Set<RoleEntity>();

	public DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();

	public DbSet<SnapshotEntity> Snapshots => Set<SnapshotEntity>();

	public DbSet<TransactionEntity> Transactions => Set<TransactionEntity>();

	public DbSet<TransferEntity> Transfers => Set<TransferEntity>();

	public DbSet<UnresolvableEventEntity> UnresolvableEvents => Set<UnresolvableEventEntity>();

	public DbSet<UserEntity> Users => Set<UserEntity>();

	public DbSet<UserPermissionEntity> UserPermissions => Set<UserPermissionEntity>();

	public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();

	public DbSet<UserSessionEntity> UserSessions => Set<UserSessionEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
		=> modelBuilder.ApplyConfigurationsFromAssembly(assembly: typeof(FinanceTrackerContext).Assembly);
}
