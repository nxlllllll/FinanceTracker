INSERT INTO transfer_statuses(code, description)
VALUES ('cancelled', 'Cancelled by the user; whatever had been applied was reversed')
ON CONFLICT DO NOTHING;
