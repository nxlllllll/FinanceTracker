using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transactions.Commands.ChangeTransactionCategory;

public sealed record ChangeTransactionCategoryCommand(
	Guid UserId,
	Guid TransactionId,
	Guid CategoryId
) : IRequest<Result<Guid, DomainException>>, IAuthorizable;