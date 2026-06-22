alter table unresolvable_events
    add column acknowledged_at timestamptz null,
    add column resolved_at timestamptz null;

create index idx_unresolvable_events_unacknowledged
    on unresolvable_events (occurred_at)
    where acknowledged_at is null and resolved_at is null;

create index idx_unresolvable_events_unresolved
    on unresolvable_events (occurred_at)
    where resolved_at is null;