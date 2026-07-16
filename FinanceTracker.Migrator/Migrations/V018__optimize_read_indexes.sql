create index if not exists idx_rm_transactions_account_history on rm_transactions (account_id, user_id, occurred_at desc, id desc);

create index if not exists idx_recurring_user_history on recurring_transactions (user_id, created_at desc, id desc);

create index if not exists idx_rm_transfers_user_history on rm_transfers (user_id, occurred_at desc, id desc);