using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.User;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetUser;

public sealed class GetUserHandler(
	IUserQueryRepository userQueryRepository
) : IRequestHandler<GetUserQuery, UserReadModel?>
{
	public async Task<UserReadModel?> Handle(
		GetUserQuery query,
		CancellationToken ct = default
	) => await userQueryRepository.GetByIdAsync(userId: query.UserId, ct: ct);
}