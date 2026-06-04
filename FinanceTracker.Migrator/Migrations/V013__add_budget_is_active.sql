alter table budgets add column is_active boolean not null default true;

alter table budgets drop constraint uq_budgets_no_overlap;

alter table budgets add constraint uq_budgets_no_overlap exclude using gist (
    user_id with =,
    category_id with =,
    daterange(date_from, date_to, '[]') with &&
) where (is_active = true);

create index idx_budgets_active on budgets (user_id, is_active) where is_active = true;