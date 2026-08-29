CREATE INDEX idx_events_type_aggregate ON events (aggregate_type, aggregate_id);
DROP INDEX IF EXISTS idx_events_aggregate_type;
