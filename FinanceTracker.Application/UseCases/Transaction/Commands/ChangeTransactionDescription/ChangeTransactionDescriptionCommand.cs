using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionDescription;

public sealed record ChangeTransactionDescriptionCommand(
	Guid UserId,
	Guid TransactionId,
	string? Description
) : IRequest<Result<Guid, AppException>>, IAuthorizable, IUserScopedRequest;
