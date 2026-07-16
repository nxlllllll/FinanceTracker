create index idx_rm_transactions_pending_rate on rm_transactions (is_rate_pending) where is_rate_pending = true;

create index idx_rm_transfers_pending_rate on rm_transfers (is_rate_pending) where is_rate_pending = true;