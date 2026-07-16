alter table events drop constraint uq_events_aggregate_version;
alter table events add constraint uq_events_aggregate_version unique (aggregate_id, aggregate_type, version);