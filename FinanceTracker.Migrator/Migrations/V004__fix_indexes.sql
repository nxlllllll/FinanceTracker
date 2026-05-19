DROP INDEX idx_events_aggregate_id; 

CREATE INDEX idx_events_aggregate_lookup ON events (aggregate_id, aggregate_type, version);

CREATE INDEX idx_accounts_user ON accounts (user_id);

CREATE INDEX idx_accounts_user_archived ON accounts (user_id, is_archived);

DROP INDEX ix_processed_messages_message_id_consumer;

ALTER TABLE processed_messages ADD PRIMARY KEY (message_id, consumer_type);