alter table user_permissions add column revoked_at timestamptz;

create index idx_user_permissions_tombstones on user_permissions (revoked_at) where not is_active;
create index idx_user_roles_tombstones on user_roles (removed_at) where not is_active;
