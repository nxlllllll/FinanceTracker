alter table user_permissions
    add column last_version integer not null default 0,
    add column is_active boolean not null default true;

alter table user_roles
    add column last_version integer not null default 0,
    add column is_active boolean not null default true,
    add column assigned_by uuid,
    add column removed_at timestamptz,
    add column removed_by uuid;

drop index if exists idx_user_permissions_user_id;
create index idx_user_permissions_active on user_permissions (user_id) where is_active;

drop index if exists idx_user_roles_user_id;
create index idx_user_roles_active on user_roles (user_id) where is_active;

alter table user_roles drop constraint user_roles_role_id_fkey;

alter table user_roles add constraint user_roles_role_id_fkey
    foreign key (role_id) references roles (id) on delete cascade;
