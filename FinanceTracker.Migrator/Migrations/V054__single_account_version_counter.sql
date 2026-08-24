UPDATE accounts a
SET last_version = b.last_version
FROM rm_account_balances b
WHERE b.account_id = a.id
  AND b.last_version > a.last_version;

ALTER TABLE rm_account_balances DROP COLUMN last_version;
