create table system_role_keys
(
    key varchar(32) not null primary key,
    name varchar(100) not null
);

insert into system_role_keys (key, name) values
    ('user', 'Пользователь'),
    ('admin', 'Администратор'),
    ('root', 'Root');

create table roles
(
    id uuid default gen_random_uuid() not null primary key,
    system_key varchar(32) references system_role_keys(key),
    display_name varchar(100) not null,
    created_at timestamptz default now() not null
);

create unique index uq_roles_system_key on roles (system_key) where system_key is not null;

insert into roles (system_key, display_name) values
    ('user', 'User'),
    ('admin', 'Admin'),
    ('root', 'Root');

create table role_permissions
(
    role_id uuid not null references roles(id) on delete cascade,
    permission varchar(64) not null,
    primary key (role_id, permission)
);

create table user_roles
(
    user_id uuid not null references users(id),
    role_id uuid not null references roles(id),
    assigned_at timestamptz not null default now(),
    primary key (user_id, role_id)
);

create index idx_user_roles_user_id on user_roles (user_id);
