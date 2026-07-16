create index if not exists idx_rm_transactions_account_history on rm_transactions (account_id, user_id, occurred_at desc, id desc);

create index if not exists idx_rm_transfers_from_account_history on rm_transfers (from_account_id, user_id, occurred_at desc, id desc);

create index if not exists idx_rm_transfers_to_account_history on rm_transfers (to_account_id, user_id, occurred_at desc, id desc);