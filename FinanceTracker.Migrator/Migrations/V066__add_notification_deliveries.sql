CREATE TABLE notification_deliveries
(
    event_id          UUID        NOT NULL,
    notification_type VARCHAR(16) NOT NULL
        CONSTRAINT fk_notification_deliveries_notification_type REFERENCES notification_types (code),
    user_id           UUID        NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    delivered_at      TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (event_id, notification_type)
);
