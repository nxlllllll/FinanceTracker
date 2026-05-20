create table user_sessions
(
    id                  uuid        default gen_random_uuid() not null primary key,
    user_id             uuid                                  not null references users(id) on delete cascade,
    refresh_token_hash  varchar(64)                           not null,
    expires_at          timestamptz                           not null,
    created_at          timestamptz default now()             not null,
    revoked_at          timestamptz
);

create unique index uq_user_sessions_token_hash
    on user_sessions (refresh_token_hash);

create index idx_user_sessions_user
    on user_sessions (user_id)
    where revoked_at is null;