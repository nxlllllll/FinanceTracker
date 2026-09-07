INSERT INTO role_permissions(role_id, permission)
SELECT r.id, p.permission
FROM roles r, (VALUES
    ('unresolvableevent:read'),
    ('unresolvableevent:write')
) AS p(permission)
WHERE r.system_key = 'admin'
ON CONFLICT DO NOTHING;