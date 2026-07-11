alter table rm_transfers add column created_at timestamptz default now() not null;

drop index if exists ix_transfers_pending_credit;
create index ix_transfers_pending_credit
    on rm_transfers (status, created_at)
    include (id, from_account_id, amount_from)
    where status = 'pending_credit';
