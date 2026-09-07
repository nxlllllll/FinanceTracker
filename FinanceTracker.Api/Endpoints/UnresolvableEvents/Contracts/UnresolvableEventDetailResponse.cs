using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.ReadModels.UnresolvableEvent;

namespace FinanceTracker.Api.Endpoints.UnresolvableEvents.Contracts;

/// <summary>
/// HTTP projection of <see cref="UnresolvableEventDetail"/>, carrying the message body that caused the escalation.
/// </summary>
public sealed record UnresolvableEventDetailResponse(
	Guid Id,
	UnresolvableEventType Type,
	Guid ReferenceId,
	string Reason,
	string Payload,
	DateTimeOffset OccurredAt,
	DateTimeOffset? AcknowledgedAt,
	DateTimeOffset? ResolvedAt
) : IResponseOf<UnresolvableEventDetail, UnresolvableEventDetailResponse>
{
	public static UnresolvableEventDetailResponse FromReadModel(
		UnresolvableEventDetail readModel
	) => new UnresolvableEventDetailResponse(
		Id: readModel.Id,
		Type: readModel.Type,
		ReferenceId: readModel.ReferenceId,
		Reason: readModel.Reason,
		Payload: readModel.Payload,
		OccurredAt: readModel.OccurredAt,
		AcknowledgedAt: readModel.AcknowledgedAt,
		ResolvedAt: readModel.ResolvedAt
	);
}
