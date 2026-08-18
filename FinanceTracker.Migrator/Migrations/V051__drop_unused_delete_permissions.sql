DELETE FROM role_permissions WHERE permission IN (
    'transaction:delete',
    'budget:delete',
    'category:delete',
    'recurringtransaction:delete'
);

DELETE FROM user_permissions WHERE permission IN (
    'transaction:delete',
    'budget:delete',
    'category:delete',
    'recurringtransaction:delete'
);
