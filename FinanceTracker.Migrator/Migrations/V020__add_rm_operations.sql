create table rm_operations
(
    id uuid not null,
    user_id uuid not null references users (id) on delete cascade,
    type varchar(12) not null,
    occurred_at timestamptz not null,
    description varchar(255),
    -- transaction fields (null for transfer)
    account_id uuid,
    category_id uuid,
    amount numeric(18,2),
    currency_code varchar(3),
    direction_type varchar(10),
    is_excluded boolean,
    -- transfer fields (null for transaction)
    from_account_id uuid,
    to_account_id uuid,
    amount_from numeric(18,2),
    currency_from varchar(3),
    amount_to numeric(18,2),
    currency_to varchar(3),
    status varchar(20),

    primary key (user_id, occurred_at, id)
);

create index idx_rm_operations_user_history on rm_operations (user_id, occurred_at desc, id desc);
create index idx_rm_operations_user_type on rm_operations (user_id, type, occurred_at desc, id desc);
create index idx_rm_operations_user_direction
    on rm_operations (user_id, direction_type, occurred_at desc, id desc)
    where direction_type is not null;