using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Transfer;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transfer.Queries.GetTransfer;

public sealed record GetTransferQuery(
	Guid TransferId,
	Guid UserId
) : IRequest<Result<TransferReadModel, AppException>>, IUserScopedRequest;
