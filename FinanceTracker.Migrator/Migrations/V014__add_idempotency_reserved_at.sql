alter table idempotent_commands add column reserved_at timestamptz not null default now();
alter table idempotent_commands drop column created_at;