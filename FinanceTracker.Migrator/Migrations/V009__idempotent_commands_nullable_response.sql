alter table idempotent_commands
    alter column response_json drop not null;