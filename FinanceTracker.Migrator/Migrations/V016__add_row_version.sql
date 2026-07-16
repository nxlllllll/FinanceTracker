alter table budgets add column row_version int not null default 0;
alter table categories add column row_version int not null default 0;
alter table rm_transactions add column row_version int not null default 0;
alter table rm_transfers add column row_version int not null default 0;
alter table users add column row_version int not null default 0;
alter table recurring_transactions add column row_version int not null default 0;
alter table rm_budget_progress add column row_version int not null default 0;
alter table rm_category_totals add column row_version int not null default 0;