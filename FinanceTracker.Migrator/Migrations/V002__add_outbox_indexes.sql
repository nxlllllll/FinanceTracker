create index if not exists idx_outbox_messages_pending
    on outbox_messages (updated_at)
    where processed_at is null and failed_at is null;

create index if not exists idx_outbox_messages_dead_letters
    on outbox_messages (failed_at)
    where failed_at is not null;