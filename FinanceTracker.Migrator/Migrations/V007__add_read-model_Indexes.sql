create index if not exists idx_rm_transactions_account_id on rm_transactions (account_id);

create index if not exists idx_rm_transactions_user_occurred on rm_transactions (user_id, occurred_at desc);

create index if not exists idx_rm_transfers_from_account_id on rm_transfers (from_account_id);

create index if not exists idx_rm_transfers_to_account_id on rm_transfers (to_account_id);

create index if not exists idx_rm_transfers_user_id on rm_transfers (user_id);

create index if not exists idx_categories_user_id on categories (user_id);