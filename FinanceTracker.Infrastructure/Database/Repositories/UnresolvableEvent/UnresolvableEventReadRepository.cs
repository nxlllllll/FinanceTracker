using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.UnresolvableEvent;

public sealed class UnresolvableEventReadRepository(FinanceTrackerContext context) : IUnresolvableEventReadRepository
{
    public async Task<IReadOnlyList<Core.ReadModels.UnresolvableEvent>> GetBatchAsync(
        int batchSize,
        DateTimeOffset? cursor = null,
        CancellationToken ct = default)
    {
        return await context.UnresolvableEvents.AsNoTracking().Where(predicate: e => cursor == null || e.OccurredAt > cursor)
            .OrderBy(keySelector: e => e.OccurredAt)
            .Take(count: batchSize)
            .Select(selector: e => new Core.ReadModels.UnresolvableEvent(
                Id: e.Id,
                Type: e.Type,
                ReferenceId: e.ReferenceId,
                Reason: e.Reason,
                OccurredAt: e.OccurredAt
            )).ToListAsync(cancellationToken: ct);
    }
}