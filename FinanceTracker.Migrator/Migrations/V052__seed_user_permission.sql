INSERT INTO role_permissions (role_id, permission)
SELECT r.id, p.permission
FROM roles r
         CROSS JOIN (VALUES ('user:read'), ('user:write')) AS p(permission)
WHERE r.system_key = 'user'
ON CONFLICT DO NOTHING;
