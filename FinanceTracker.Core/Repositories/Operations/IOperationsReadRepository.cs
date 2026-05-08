using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Core.Repositories.Operations;

public interface IOperationsReadRepository
{
	Task<IReadOnlyList<OperationDto>> GetHistoryAsync(
		Guid userId,
		OperationFilterType? type = null,
		DateTime? dateFrom = null,
		DateTime? dateTo = null,
		DateTime? cursorOccurredAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default
	);
}