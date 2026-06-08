ALTER TABLE idempotent_commands ADD COLUMN reserved_at timestamptz NOT NULL DEFAULT now();

ALTER TABLE idempotent_commands DROP COLUMN created_at;