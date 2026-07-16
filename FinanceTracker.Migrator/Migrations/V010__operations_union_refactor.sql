drop table if exists rm_operations;

drop index if exists idx_rm_transactions_user_occurred;
create index idx_rm_transactions_user_history on rm_transactions (user_id, occurred_at desc, id desc);

drop index if exists idx_rm_transfers_user_id;
create index idx_rm_transfers_user_history on rm_transfers (user_id, occurred_at desc, id desc);