ALTER TABLE users ADD COLUMN time_zone_id VARCHAR(64) DEFAULT 'Etc/UTC' NOT NULL
    CONSTRAINT users_time_zone_id_check CHECK (time_zone_id ~ '^[A-Za-z0-9+_/-]+$');

ALTER TABLE recurring_transactions ADD COLUMN next_due_at_utc TIMESTAMPTZ NOT NULL;

DROP INDEX IF EXISTS idx_recurring_active;

CREATE INDEX idx_recurring_next_due ON recurring_transactions (next_due_at_utc)
    WHERE is_active = true;
