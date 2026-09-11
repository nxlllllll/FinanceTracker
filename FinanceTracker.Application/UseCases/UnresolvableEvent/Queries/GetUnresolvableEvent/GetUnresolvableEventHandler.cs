using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels.UnresolvableEvent;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.UnresolvableEvent.Queries.GetUnresolvableEvent;

public sealed class GetUnresolvableEventHandler(
	IUnresolvableEventReadRepository unresolvableEventReadRepository
) : IRequestHandler<GetUnresolvableEventQuery, Result<UnresolvableEventDetail, AppException>>
{
	public async Task<Result<UnresolvableEventDetail, AppException>> Handle(
		GetUnresolvableEventQuery query,
		CancellationToken ct = default)
	{
		UnresolvableEventDetail? model = await unresolvableEventReadRepository.GetByIdAsync(
			eventId: query.EventId,
			ct: ct
		);

		if (model is null)
			return Result<UnresolvableEventDetail, AppException>.Failure(error: new NotFoundException(message: "Unresolvable event not found.", id: query.EventId));

		return Result<UnresolvableEventDetail, AppException>.Success(value: model);
	}
}
