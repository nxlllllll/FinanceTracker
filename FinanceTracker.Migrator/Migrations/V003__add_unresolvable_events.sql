create table unresolvable_event_types (
    code varchar(50) primary key,
    name varchar(100) not null
);

insert into unresolvable_event_types (code, name) values
    ('outbox_dead_letter',   'Outbox Dead Letter'),
    ('transfer_compensation', 'Transfer Compensation Failure'),
    ('consumer_dead_letter', 'Consumer Dead Letter');

create table unresolvable_events (
    id           uuid        primary key,
    type_code    varchar(50) not null references unresolvable_event_types(code),
    reference_id uuid        not null,
    reason       text        not null,
    payload      jsonb       not null,
    occurred_at  timestamptz not null
);

create index idx_unresolvable_events_occurred_at on unresolvable_events (occurred_at desc);