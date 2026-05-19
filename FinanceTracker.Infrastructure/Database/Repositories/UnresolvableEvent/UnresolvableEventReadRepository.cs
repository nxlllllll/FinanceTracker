using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.UnresolvableEvent;

public sealed class UnresolvableEventReadRepository(
    FinanceTrackerContext context
) : IUnresolvableEventReadRepository
{
    public async Task<IReadOnlyList<UnresolvableEventDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.UnresolvableEvents.AsNoTracking()
            .OrderBy(keySelector: e => e.OccurredAt)
            .Select(selector: e => new UnresolvableEventDto(
                Id: e.Id,
                Type: e.Type,
                ReferenceId: e.ReferenceId,
                Reason: e.Reason,
                OccurredAt: e.OccurredAt
            ))
            .ToListAsync(cancellationToken: ct);
    }
}