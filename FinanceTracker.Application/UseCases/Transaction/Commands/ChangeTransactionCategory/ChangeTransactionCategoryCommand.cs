using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionCategory;

public sealed record ChangeTransactionCategoryCommand(
	Guid UserId,
	Guid TransactionId,
	Guid CategoryId
) : IRequest<Result<Guid, AppException>>, IAuthorizable, IUserScopedRequest;
