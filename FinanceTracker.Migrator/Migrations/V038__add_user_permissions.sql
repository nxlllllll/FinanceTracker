create table user_permissions
(
    user_id uuid not null,
    permission varchar(64) not null,
    granted_at timestamptz not null,
    primary key (user_id, permission)
);

create index idx_user_permissions_user_id on user_permissions (user_id);
