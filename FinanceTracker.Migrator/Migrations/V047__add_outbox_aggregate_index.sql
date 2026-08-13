create index if not exists idx_outbox_pending_aggregate
    on outbox_messages (aggregate_id, id)
    where processed_at is null and failed_at is null;
