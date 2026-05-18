using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Commands.ChangeUserBaseCurrency;

public sealed record ChangeUserBaseCurrencyCommand(
	Guid UserId,
	Currency NewBaseCurrency
) : IRequest<Result<Guid, DomainException>>, IAuthorizable;