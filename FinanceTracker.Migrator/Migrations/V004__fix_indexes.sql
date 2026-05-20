drop index idx_events_aggregate_id; 

create index idx_events_aggregate_lookup ON events (aggregate_id, aggregate_type, version);

create index idx_accounts_user ON accounts (user_id);

create index idx_accounts_user_archived ON accounts (user_id, is_archived);

drop index ix_processed_messages_message_id_consumer;

alter table processed_messages add primary key (message_id, consumer_type);