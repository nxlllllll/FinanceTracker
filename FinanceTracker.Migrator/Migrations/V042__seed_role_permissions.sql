insert into role_permissions (role_id, permission)
select r.id, p.permission
from roles r cross join (values
    ('account:read'),
    ('account:write'),
    ('balance:read'),
    ('transaction:read'),
    ('transaction:write'),
    ('transaction:delete'),
    ('budget:read'),
    ('budget:write'),
    ('budget:delete'),
    ('category:read'),
    ('category:write'),
    ('category:delete'),
    ('recurringtransaction:read'),
    ('recurringtransaction:write'),
('recurringtransaction:delete')
) as p(permission)
where r.system_key = 'user'
on conflict do nothing;

insert into role_permissions (role_id, permission)
select r.id, 'permission:manage'
from roles r
where r.system_key = 'admin'
on conflict do nothing;
