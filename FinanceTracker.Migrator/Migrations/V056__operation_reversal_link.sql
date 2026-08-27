ALTER TABLE rm_operations ADD COLUMN is_reverted boolean NOT NULL DEFAULT false;
ALTER TABLE rm_operations ADD COLUMN reversal_of_id uuid;

ALTER TABLE rm_operations ADD CONSTRAINT rm_operations_reversal_not_self_check
    CHECK (reversal_of_id IS NULL OR reversal_of_id <> id);

CREATE INDEX idx_rm_operations_reversal_of ON rm_operations (reversal_of_id) WHERE reversal_of_id IS NOT NULL;
