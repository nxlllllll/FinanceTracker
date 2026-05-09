using FinanceTracker.Application.UseCases.Transactions.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transactions.Services;
using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using ZLogger;

namespace FinanceTracker.Worker.RecurringTransactionProjection.Consumers;

public sealed class RecurringTransactionConsumer(
    IAccountRepository accountRepository,
    ITransactionCreationService transactionCreationService,
    IRecurringTransactionReadRepository recurringTransactionReadRepository,
    ILogger<RecurringTransactionConsumer> logger)
{
    public async Task HandleAsync(RecurringTransactionTriggeredMessage message, CancellationToken ct)
    {
        Core.Domains.RecurringTransaction.RecurringTransaction? recurringTransaction = await recurringTransactionReadRepository.GetByIdAsync(
            recurringTransactionId: message.RecurringTransactionId,
            ct: ct
        );

        if (recurringTransaction is null)
        {
            logger.ZLogWarning(message: $"Recurring transaction {message.RecurringTransactionId} not found, skipping.");
            return;
        }

        Core.Domains.Account.Account? account = await accountRepository.GetByIdAsync(accountId: message.AccountId, ct: ct);

        if (account is null)
        {
            logger.ZLogError(message: $"Account {message.AccountId} not found while processing recurring transaction {message.RecurringTransactionId}.");
            throw new NotFoundException(message: "Account not found.", id: message.AccountId);
        }

        Result<Currency, DomainException> currencyResult = Currency.Create(value: message.Currency);
        if (currencyResult.IsFailure)
            throw new NotFoundException(message: $"Invalid currency: {message.Currency}", id: message.RecurringTransactionId);

        await transactionCreationService.CreateAsync(command: new CreateTransactionCommand(
            AccountId: message.AccountId,
            UserId: message.UserId,
            CategoryId: message.CategoryId,
            Amount: message.Amount,
            Currency: currencyResult.Value,
            Direction: Enum.Parse<Core.Domains.Account.DirectionType>(value: message.Direction),
            Description: message.Description,
            OccurredAt: message.OccurredAt
        ), account: account, ct: ct);

        logger.ZLogInformation(message: $"Created transaction for recurring transaction {message.RecurringTransactionId}.");
    }
}