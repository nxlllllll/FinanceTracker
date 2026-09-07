using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using UnresolvableEventReadModel = FinanceTracker.Core.ReadModels.UnresolvableEvent.UnresolvableEvent;

namespace FinanceTracker.Api.Endpoints.UnresolvableEvents.Contracts;

/// <summary>HTTP projection of an escalated event.</summary>
public sealed record UnresolvableEventResponse(
	Guid Id,
	UnresolvableEventType Type,
	Guid ReferenceId,
	string Reason,
	DateTimeOffset OccurredAt,
	DateTimeOffset? AcknowledgedAt,
	DateTimeOffset? ResolvedAt
) : IResponseOf<UnresolvableEventReadModel, UnresolvableEventResponse>
{
	public static UnresolvableEventResponse FromReadModel(
		UnresolvableEventReadModel readModel
	) => new UnresolvableEventResponse(
		Id: readModel.Id,
		Type: readModel.Type,
		ReferenceId: readModel.ReferenceId,
		Reason: readModel.Reason,
		OccurredAt: readModel.OccurredAt,
		AcknowledgedAt: readModel.AcknowledgedAt,
		ResolvedAt: readModel.ResolvedAt
	);
}
