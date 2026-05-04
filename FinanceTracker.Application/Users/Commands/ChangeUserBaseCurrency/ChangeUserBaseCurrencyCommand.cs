using FinanceTracker.Application.Behaviours.Authorization;
using MediatR;

namespace FinanceTracker.Application.Users.Commands.ChangeUserBaseCurrency;

public sealed record ChangeUserBaseCurrencyCommand(
	Guid UserId,
	string NewBaseCurrency
) : IRequest<Guid>, IAuthorizable;