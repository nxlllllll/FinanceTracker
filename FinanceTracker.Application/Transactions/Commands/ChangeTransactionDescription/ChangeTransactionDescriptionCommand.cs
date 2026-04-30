using FinanceTracker.Application.Behaviours.Authorization;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.ChangeTransactionDescription;

public sealed record ChangeTransactionDescriptionCommand(
	Guid UserId,
	Guid TransactionId,
	string? Description
) : IRequest, IAuthorizable;