drop index if exists idx_recurring_active;

create index idx_recurring_active
    on recurring_transactions (is_active, day_of_month, last_executed_at)
    where is_active = true;