using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetUser;

public sealed class GetUserHandler(
	IUserQueryRepository userQueryRepository
) : IRequestHandler<GetUserQuery, Result<UserReadModel, DomainException>>
{
	public async Task<Result<UserReadModel, DomainException>> Handle(
		GetUserQuery query,
		CancellationToken ct = default)
	{
		UserReadModel? model = await userQueryRepository.GetByIdAsync(
			userId: query.UserId,
			ct: ct
		);

		if (model is null)
			return Result<UserReadModel, DomainException>.Failure(error: new NotFoundException(message: "User not found.", id: query.UserId));

		return Result<UserReadModel, DomainException>.Success(value: model);
	}
}