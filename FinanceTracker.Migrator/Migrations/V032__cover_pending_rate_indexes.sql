drop index if exists idx_rm_transactions_pending_rate;
create index idx_rm_transactions_pending_rate on rm_transactions (is_rate_pending)
    include (id, account_id, amount, currency_code, base_currency_code, exchange_rate, direction_type, row_version, occurred_at)
    where is_rate_pending = true;

drop index if exists idx_rm_transfers_pending_rate;
create index idx_rm_transfers_pending_rate on rm_transfers (is_rate_pending)
    include (id, from_account_id, to_account_id, amount_from, currency_from, currency_to, exchange_rate, row_version, occurred_at)
    where is_rate_pending = true;