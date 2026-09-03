using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Transfer;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transfer.Queries.GetTransfers;

public sealed record GetTransfersQuery(
	Guid UserId,
	Guid? AccountId = null,
	TransferStatus? Status = null,
	DateTimeOffset? DateFrom = null,
	DateTimeOffset? DateTo = null,
	DateTimeOffset? CursorOccurredAt = null,
	Guid? CursorId = null,
	int PageSize = 20
) : IRequest<Result<PagedResult<TransferReadModel>, AppException>>, IUserScopedRequest;
