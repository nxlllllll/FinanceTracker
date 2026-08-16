alter table rm_transactions             drop constraint if exists rm_transactions_account_id_fkey;
alter table rm_transfers                drop constraint if exists rm_transfers_from_account_id_fkey;
alter table rm_transfers                drop constraint if exists rm_transfers_to_account_id_fkey;
alter table recurring_transactions      drop constraint if exists recurring_transactions_account_id_fkey;
