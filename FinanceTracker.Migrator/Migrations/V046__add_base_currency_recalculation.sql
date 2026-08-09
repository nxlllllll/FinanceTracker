create table base_currency_recalculation_statuses
(
    status varchar(16) not null primary key,
    description text not null
);

insert into base_currency_recalculation_statuses (status, description) values
('pending', 'The base currency has changed and category totals no longer match it. Nothing has been recomputed yet; reads report totals as unavailable rather than showing amounts in the previous currency.'),
('in_progress', 'A worker holds a lease on this row and is rebuilding the totals. Reads still report them as unavailable.'),
('completed', 'Totals match the current base currency. Kept rather than deleted so the last run stays inspectable.'),
('failed', 'Retried up to the configured limit and gave up. Totals stay unavailable and will not be retried automatically — this needs a look.');

create table user_base_currency_recalculations
(
    user_id uuid not null primary key references users (id) on delete cascade,
    status varchar(16) not null
        constraint fk_user_base_currency_recalculations_status references base_currency_recalculation_statuses (status),
    target_currency char(3) not null references currencies (code),
    requested_at timestamptz not null,
    locked_until timestamptz,
    attempts integer not null default 0,
    last_error text
);

create index idx_user_base_currency_recalculations_claimable
    on user_base_currency_recalculations (requested_at)
    where status in ('pending', 'in_progress');
