drop index if exists idx_rm_transactions_pending_rate;

create index idx_rm_transactions_pending_rate
    on rm_transactions (is_rate_pending)
    include (account_id, amount, currency_code, base_currency_code, exchange_rate, direction_type, row_version, occurred_at)
    where is_rate_pending = true;