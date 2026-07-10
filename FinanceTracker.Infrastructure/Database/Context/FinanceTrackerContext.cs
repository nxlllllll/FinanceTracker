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
using FinanceTracker.Infrastructure.Database.Context.Transaction;
using FinanceTracker.Infrastructure.Database.Context.Transfer;
using FinanceTracker.Infrastructure.Database.Context.UnresolvableEvent;
using FinanceTracker.Infrastructure.Database.Context.User;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Context;

public sealed class FinanceTrackerContext(DbContextOptions<FinanceTrackerContext> options) : DbContext(options)
{
	public DbSet<EventEntity> Events => Set<EventEntity>();

	public DbSet<AccountEntity> Accounts => Set<AccountEntity>();

	public DbSet<AccountBalanceEntity> AccountBalances => Set<AccountBalanceEntity>();

	public DbSet<AccountBalanceAppliedEventEntity> AccountBalanceAppliedEvents => Set<AccountBalanceAppliedEventEntity>();

	public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
	public DbSet<TransactionEntity> Transactions => Set<TransactionEntity>();

	public DbSet<CurrencyEntity> Currencies => Set<CurrencyEntity>();

	public DbSet<UserEntity> Users => Set<UserEntity>();

	public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

	public DbSet<CurrencyRateEntity> CurrencyRates => Set<CurrencyRateEntity>();

	public DbSet<SnapshotEntity> Snapshots => Set<SnapshotEntity>();

	public DbSet<TransferEntity> Transfers => Set<TransferEntity>();

	public DbSet<CategoryTotalEntity> CategoryTotals => Set<CategoryTotalEntity>();

	public DbSet<BudgetEntity> Budgets => Set<BudgetEntity>();

	public DbSet<BudgetProgressEntity> BudgetProgresses => Set<BudgetProgressEntity>();

	public DbSet<RecurringTransactionEntity> RecurringTransactions => Set<RecurringTransactionEntity>();

	public DbSet<ProcessedMessageEntity> ProcessedMessages => Set<ProcessedMessageEntity>();

	public DbSet<IdempotentCommandEntity> IdempotentCommands => Set<IdempotentCommandEntity>();

	public DbSet<UnresolvableEventEntity> UnresolvableEvents => Set<UnresolvableEventEntity>();

	public DbSet<UserSessionEntity> UserSessions => Set<UserSessionEntity>();

	public DbSet<OperationEntity> Operations => Set<OperationEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
		=> modelBuilder.ApplyConfigurationsFromAssembly(assembly: typeof(FinanceTrackerContext).Assembly);
}
