using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transactions.Commands.ChangeTransactionDescription;

public sealed record ChangeTransactionDescriptionCommand(
	Guid UserId,
	Guid TransactionId,
	string? Description
) : IRequest<Result<Guid, DomainException>>, IAuthorizable, IUserScopedRequest;
