using MediatR;

namespace FinanceTracker.Application.Users.Commands.ChangeUserBaseCurrency;

public record ChangeUserBaseCurrencyCommand(
	Guid UserId,
	string NewBaseCurrency
) : IRequest;