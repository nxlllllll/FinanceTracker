CREATE TABLE notification_types
(
    code        VARCHAR(16) NOT NULL PRIMARY KEY,
    description TEXT        NOT NULL
);

INSERT INTO notification_types (code, description) VALUES
('email', 'Notifications are sent to the address the user signs in with.');

ALTER TABLE users ADD COLUMN notification_type VARCHAR(16) NULL DEFAULT 'email'
    CONSTRAINT fk_users_notification_type REFERENCES notification_types (code);
