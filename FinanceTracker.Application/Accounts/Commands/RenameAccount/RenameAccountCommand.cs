using FinanceTracker.Core.Domains.Account;
using MediatR;

namespace FinanceTracker.Application.Accounts.Commands.RenameAccount;

public sealed record RenameAccountCommand(
	Guid AccountId,
	string NewName
) : IRequest;