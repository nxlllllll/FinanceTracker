using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Commands.ChangeUserBaseCurrency;

public sealed record ChangeUserBaseCurrencyCommand(
	Guid UserId,
	string NewBaseCurrency
) : IRequest<Result<Guid, DomainException>>, IAuthorizable;