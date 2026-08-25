ALTER TABLE rm_transactions ADD COLUMN created_at timestamptz NOT NULL;

ALTER TABLE rm_transactions ADD COLUMN is_cancelled boolean NOT NULL DEFAULT false;
ALTER TABLE rm_transactions ADD COLUMN cancelled_at timestamptz;

ALTER TABLE rm_transactions ADD CONSTRAINT rm_transactions_cancelled_at_check
    CHECK ((is_cancelled AND cancelled_at IS NOT NULL) OR (NOT is_cancelled AND cancelled_at IS NULL));
