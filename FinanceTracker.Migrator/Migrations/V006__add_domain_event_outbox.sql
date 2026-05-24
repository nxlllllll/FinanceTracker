-- ============================================================
-- DOMAIN EVENT OUTBOX
-- Outbox для non-ES агрегатов (User, Budget, Category и т.д.)
-- ============================================================
create table domain_event_outbox
(
    id             uuid        default gen_random_uuid() not null primary key,
    event_type     varchar(100)                          not null,
    aggregate_id   uuid                                  not null,
    aggregate_type varchar(50)                           not null,
    correlation_id uuid,
    payload        jsonb                                 not null,
    occurred_at    timestamptz                           not null,
    created_at     timestamptz default now()             not null,
    processed_at   timestamptz,
    retry_count    integer     default 0                 not null,
    failed_at      timestamptz
);

create index idx_domain_event_outbox_pending
    on domain_event_outbox (created_at)
    where processed_at is null and failed_at is null;

create index idx_domain_event_outbox_correlation_id
    on domain_event_outbox (correlation_id)
    where correlation_id is not null;

create index idx_domain_event_outbox_processed
    on domain_event_outbox (processed_at)
    where processed_at is not null;

create index idx_domain_event_outbox_failed
    on domain_event_outbox (failed_at)
    where failed_at is not null;