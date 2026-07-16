create table rm_account_balance_applied_events
(
    account_id uuid not null references accounts (id),
    version integer not null,
    applied_at timestamptz not null,
    primary key (account_id, version)
);

create index idx_rm_account_balance_applied_events_applied_at on rm_account_balance_applied_events(applied_at);
