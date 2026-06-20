alter table idempotent_commands add column user_id uuid not null default '00000000-0000-0000-0000-000000000000';
alter table idempotent_commands drop constraint idempotent_commands_pkey;
alter table idempotent_commands add primary key (idempotency_key, command_type, user_id);