using System.Text.Json;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Operation;
using FinanceTracker.Core.Repositories.Operation;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Operation;
using FinanceTracker.Infrastructure.Database.Extensions;
using FinanceTracker.Infrastructure.Database.Repositories.User;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Operation;

public sealed class OperationsWriteRepository(
    FinanceTrackerContext context
) : IOperationsWriteRepository
{
    public async Task CreateFromTransactionAsync(Core.Domains.Transaction.Transaction transaction, CancellationToken ct = default)
    {
        await context.Operations.AddAsync(entity: new OperationEntity
        {
            Id = transaction.Id,
            UserId = transaction.UserId,
            Type = OperationType.Transaction,
            OccurredAt = transaction.OccurredAt,
            Description = transaction.Description,
            Payload = JsonSerializer.Serialize(value: new TransactionPayload(
                AccountId: transaction.AccountId,
                CategoryId: transaction.CategoryId,
                Amount: transaction.Amount.Amount,
                Currency: transaction.Amount.Currency,
                Direction: transaction.Direction,
                IsExcluded: transaction.IsExcluded
            ), options: FinanceTrackerJsonOptions.Payload)
        }, cancellationToken: ct);

        await context.SaveChangesAsync(cancellationToken: ct);
    }

    public async Task CreateFromTransferAsync(Core.Domains.Transfer.Transfer transfer, CancellationToken ct = default)
    {
        await context.Operations.AddAsync(entity: new OperationEntity
        {
            Id = transfer.Id,
            UserId = transfer.UserId,
            Type = OperationType.Transfer,
            OccurredAt = transfer.OccurredAt,
            Description = transfer.Description,
            Payload = JsonSerializer.Serialize(value: new TransferPayload(
                FromAccountId: transfer.FromAccountId,
                ToAccountId: transfer.ToAccountId,
                AmountFrom: transfer.AmountFrom.Amount,
                CurrencyFrom: transfer.AmountFrom.Currency,
                AmountTo: transfer.AmountTo.Amount,
                CurrencyTo: transfer.AmountTo.Currency
            ), options: FinanceTrackerJsonOptions.Payload)
        }, cancellationToken: ct);

        await context.SaveChangesAsync(cancellationToken: ct);
    }

    public async Task UpdateCategoryAsync(Guid operationId, Guid categoryId, CancellationToken ct = default)
    {
        await context.ChangePayloadAsync<OperationEntity, TransactionPayload, Guid>(
            id: operationId,
            property: payload => payload.CategoryId,
            value: categoryId,
            ct: ct
        );
    }

    public async Task UpdateIsExcludedAsync(Guid operationId, bool isExcluded, CancellationToken ct = default)
    {
        await context.ChangePayloadAsync<OperationEntity, TransactionPayload, bool>(
            id: operationId,
            property: payload => payload.IsExcluded,
            value: isExcluded,
            ct: ct
        );
    }

    public async Task UpdateDescriptionAsync(Guid operationId, string? description, CancellationToken ct = default)
    {
        await context.Operations.Where(predicate: o => o.Id == operationId).ExecuteUpdateAsync(
            setPropertyCalls: s => s.SetProperty(o => o.Description, description),
            cancellationToken: ct
        );
    }
}
