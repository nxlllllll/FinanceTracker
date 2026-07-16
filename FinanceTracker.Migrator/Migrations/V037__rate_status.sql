create table rate_statuses
(
    status varchar(16) not null primary key,
    description text not null
);

insert into rate_statuses (status, description) values
    ('exact', 'The exact course on the date of the operation was immediately known. There is nothing to adjust.'),
    ('pending', 'There is no exchange rate as of the date of the operation yet, a temporary one is being used. The only unclosed condition: BalanceAdjustmentJob will replace the exchange rate with the current one and make the difference on the balance.'),
    ('resolved', 'The exchange rate has arrived, and the difference is based on the balance. The operation is recorded according to the current rate.'),
    ('approximated', 'The exact course will not arrive — as a rule, the operation is dated in the past, and the courses are collected only for the current day. The temporary course has been accepted as final.'),
    ('unresolvable', 'The course arrived, but the correction did not apply. Escalated to unresolvable_events, not automatically repeated.'),
    ('cancelled', 'The operation was canceled before the course was adjusted. Correction is not needed: the money movement that it would have corrected has not taken place.');

alter table rm_transactions
    add column rate_status varchar(16) not null default 'exact'
        constraint fk_rm_transactions_rate_status references rate_statuses(status),
    add column rate_status_changed_at timestamptz not null;

alter table rm_transactions
    drop column is_rate_pending;

alter table rm_transfers
    add column rate_status varchar(16) not null default 'exact'
        constraint fk_rm_transfers_rate_status references rate_statuses(status),
    add column rate_status_changed_at timestamptz not null;

alter table rm_transfers
    drop column is_rate_pending;

drop index if exists idx_rm_transactions_pending_rate;
create index idx_rm_transactions_pending_rate on rm_transactions (occurred_at, id)
    include (user_id, currency_code, base_currency_code, rate_status_changed_at)
    where rate_status = 'pending';

drop index if exists idx_rm_transfers_pending_rate;
create index idx_rm_transfers_pending_rate on rm_transfers (occurred_at, id)
    include (currency_from, currency_to, rate_status_changed_at)
    where rate_status = 'pending';
