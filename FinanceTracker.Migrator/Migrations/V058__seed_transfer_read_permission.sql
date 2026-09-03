INSERT INTO role_permissions (role_id, permission)
SELECT r.id, 'transfer:read'
FROM roles r
WHERE r.system_key = 'user'
    ON CONFLICT DO NOTHING;
