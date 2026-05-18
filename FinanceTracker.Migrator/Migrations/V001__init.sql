-- ============================================================
-- FINANCE TRACKER — FULL SCHEMA
-- Architecture: CQRS + Event Sourcing
-- Convention: rm_ prefix = read model (projection)
-- ============================================================


-- ============================================================
-- Справочники
-- ============================================================
create table currencies
(
    code        char(3)              not null primary key constraint currencies_code_check check (code ~ '^[A-Z]{3}$'),
    name        varchar(50)          not null,
    symbol      varchar(5)           not null,
    is_active   boolean default true not null
);

insert into currencies (code, name, symbol) values
    ('RUB', 'Российский рубль', '₽'),
    ('USD', 'Доллар США',       '$'),
    ('EUR', 'Евро',             '€');


create table account_types
(
    type        varchar(20)  not null primary key,
    name        varchar(100) not null,
    description varchar(255)
);

insert into account_types (type, name, description) values
    ('checking', 'Расчетный счёт',     'Расчётный счёт в банке, повседневные операции'),
    ('savings',  'Накопительный счёт', 'Счёт для накоплений и целей'),
    ('cash',     'Наличные',           'Бумажные деньги, кошелёк');


create table category_types
(
    type varchar(10) not null primary key,
    name varchar(50) not null
);

insert into category_types (type, name) values
    ('income',  'Доход'),
    ('expense', 'Расход');


create table direction_types
(
    type varchar(10) not null primary key,
    name varchar(50) not null
);

insert into direction_types (type, name) values
    ('credit', 'Пополнение'),
    ('debit',  'Списание');


-- ============================================================
-- Курс валют на дату
-- ============================================================

create table currency_rates
(
    base_code   char(3)        not null references currencies(code),
    target_code char(3)        not null references currencies(code),
    rate        numeric(18, 6) not null
        constraint currency_rates_rate_check check (rate > 0),
    actual_at   date           not null,
    created_at  timestamptz    default now() not null,
    primary key (base_code, target_code, actual_at)
);

create index idx_currency_rates_lookup
    on currency_rates (base_code, target_code, actual_at desc);


-- ============================================================
-- USERS
-- ============================================================

create table users
(
    id                   uuid        default gen_random_uuid() not null primary key,
    email                varchar(255)                          not null
        constraint uq_users_email unique
        constraint users_email_check check (email ~ '^[^@\s]+@[^@\s]+\.[^@\s]+$'),
    password_hash        varchar(255)                          not null,
    base_currency_code   char(3)                               not null references currencies(code),
    created_at           timestamptz default now()             not null
);


-- ============================================================
-- ACCOUNTS
-- Только метаданные — баланс живёт в rm_account_balances
-- initial_balance передаётся в payload события AccountCreated
-- ============================================================

create table accounts
(
    id                uuid        default gen_random_uuid() not null primary key,
    user_id           uuid                                  not null references users(id),
    name              varchar(100)                          not null,
    account_type_code varchar(20)                           not null references account_types(type),
    currency_code     char(3)                               not null references currencies(code),
    is_archived       boolean     default false             not null,
    created_at        timestamptz default now()             not null
);


-- ============================================================
-- CATEGORIES
-- Иерархия через parent_id (self-reference)
-- ============================================================

create table categories
(
    id          uuid        default gen_random_uuid() not null primary key,
    user_id     uuid                                  not null references users(id),
    parent_id   uuid                                  references categories(id),
    name        varchar(100)                          not null,
    type_code   varchar(10)                           not null references category_types(type),
    created_at  timestamptz default now()             not null,
    is_archived boolean                               not null default false
);


-- ============================================================
-- EVENT STORE
-- ============================================================

create table events
(
    id             uuid        default gen_random_uuid() not null primary key,
    aggregate_id   uuid                                  not null,
    aggregate_type varchar(50)                           not null,
    event_type     varchar(100)                          not null,
    version        integer                               not null,
    correlation_id uuid,
    payload        jsonb                                 not null,
    occurred_at    timestamptz                           not null,
    created_at     timestamptz default now()             not null,
    schema_version integer     default 1                 not null,
    constraint uq_events_aggregate_version unique (aggregate_id, version)
);

create index idx_events_aggregate_id
    on events (aggregate_id, version);

create index idx_events_correlation_id
    on events (correlation_id)
    where correlation_id is not null;

-- ============================================================
-- SNAPSHOTS
-- Кеш состояния агрегата на конкретной версии.
-- ============================================================

create table snapshots
(
    aggregate_id   uuid        not null,
    aggregate_type varchar(50) not null,
    version        integer     not null,
    state          jsonb       not null,
    created_at     timestamptz default now() not null,
    primary key (aggregate_id, version)
);

-- ============================================================
-- OUTBOX
-- ============================================================

create table outbox_messages
(
    id              uuid        default gen_random_uuid() not null primary key,
    aggregate_id    uuid                                  not null,
    aggregate_type  varchar(50)                           not null,
    payload         jsonb                                 not null,
    updated_at      timestamptz default now()             not null,
    processed_at    timestamptz,
    retry_count     int         default 0                 not null,
    failed_at       timestamptz
);

create index idx_outbox_messages_unprocessed
    on outbox_messages (updated_at)
    where processed_at is null
        and failed_at  is null;

create table processed_messages
(
    message_id    uuid                                  not null,
    processed_at  timestamptz                           not null,
    consumer_type varchar(100)                          not null
);

create unique index ix_processed_messages_message_id_consumer
    on processed_messages (message_id, consumer_type);

-- ============================================================
-- READ MODELS
-- ============================================================

-- Текущий баланс счёта
create table rm_account_balances
(
    account_id   uuid           not null primary key references accounts(id),
    balance      numeric(18, 2) default 0 not null,
    last_version integer                   not null,
    updated_at   timestamptz               not null
);


-- Транзакции (доходы и расходы)
-- exchange_rate — курс на момент операции (относительно base_currency пользователя)
-- is_excluded   — не влияет на аналитику и бюджеты, но влияет на баланс
create table rm_transactions
(
    id              uuid            default gen_random_uuid()   not null    primary key,
    account_id      uuid                                        not null    references accounts(id),
    user_id         uuid                                        not null    references users(id),
    category_id     uuid                                        not null    references categories(id),
    amount          numeric(18, 2)                              not null
        constraint rm_transactions_amount_check check (amount > 0),
    currency_code   varchar(3)                                  not null    references currencies(code),
    direction_type  varchar(10)                                 not null    references direction_types(type),
    exchange_rate   numeric(18, 6)  default 1                   not null
        constraint rm_transactions_exchange_rate_check check (exchange_rate > 0),
    is_excluded     boolean         default false               not null,
    description     varchar(255),
    is_rate_pending boolean         default false               not null,
    occurred_at     timestamptz                                 not null
);

create index idx_rm_transactions_account   on rm_transactions (account_id);
create index idx_rm_transactions_user      on rm_transactions (user_id);
create index idx_rm_transactions_date      on rm_transactions (occurred_at desc);
create index idx_rm_transactions_category  on rm_transactions (category_id);
create index idx_rm_transactions_user_date on rm_transactions (user_id, occurred_at desc);


-- Переводы между своими счетами
-- Физически отделены от rm_transactions — не попадают в аналитику расходов/доходов
-- exchange_rate — курс from_currency → to_currency на момент перевода
create table rm_transfers
(
    id                  uuid            default gen_random_uuid()   not null primary key,
    user_id             uuid                                        not null references users(id),
    from_account_id     uuid                                        not null references accounts(id),
    to_account_id       uuid                                        not null references accounts(id),
    amount_from         numeric(18, 2)                              not null
        constraint rm_transfers_amount_from_check check (amount_from > 0),
    currency_from       varchar(3)                                  not null references currencies(code),
    amount_to           numeric(18, 2)                              not null
        constraint rm_transfers_amount_to_check check (amount_to > 0),
    currency_to         varchar(3)                                  not null references currencies(code),
    exchange_rate       numeric(18, 6)  default 1                   not null
        constraint rm_transfers_exchange_rate_check check (exchange_rate > 0),
    description         varchar(255),
    is_rate_pending     boolean         default false               not null,
    occurred_at         timestamptz                                 not null
);

create index idx_rm_transfers_user         on rm_transfers (user_id);
create index idx_rm_transfers_from_account on rm_transfers (from_account_id);
create index idx_rm_transfers_to_account   on rm_transfers (to_account_id);
create index idx_rm_transfers_date         on rm_transfers (occurred_at desc);


-- Итоги по категориям за период (для аналитики)
-- Учитывает только транзакции где is_excluded = false
create table rm_category_totals
(
    id                uuid           default gen_random_uuid() not null primary key,
    user_id           uuid                                     not null references users(id),
    category_id       uuid                                     not null references categories(id),
    period            date                                     not null, -- первый день месяца
    total             numeric(18, 2) default 0                 not null,
    transaction_count integer        default 0                 not null,
    updated_at        timestamptz                              not null,
    constraint uq_rm_category_totals_period unique (user_id, category_id, period)
);


-- ============================================================
-- BUDGETS
-- Произвольный период (date_from / date_to).
-- Привязан к категории.
-- Бюджет — доменная сущность, управляется через события.
-- ============================================================

create table budgets
(
    id            uuid           default gen_random_uuid() not null primary key,
    user_id       uuid                                     not null references users(id),
    category_id   uuid                                     not null references categories(id),
    amount        numeric(18, 2)                           not null
        constraint budgets_amount_check check (amount > 0),
    currency_code char(3)                                  not null references currencies(code),
    date_from     date                                     not null,
    date_to       date                                     not null,
    created_at    timestamptz    default now()             not null,
    constraint budgets_dates_check check (date_to > date_from)
);

create index idx_budgets_user     on budgets (user_id);
create index idx_budgets_category on budgets (category_id);
create index idx_budgets_period   on budgets (user_id, date_from, date_to);


-- Прогресс бюджета: потрачено vs лимит
-- Пересчитывается при каждой новой транзакции в категории
create table rm_budget_progress
(
    budget_id  uuid           not null primary key references budgets(id),
    spent      numeric(18, 2) default 0 not null,
    updated_at timestamptz              not null
);


-- ============================================================
-- RECURRING TRANSACTIONS
-- Шаблон транзакции, которая создаётся автоматически в конкретное число месяца
-- ============================================================

create table recurring_transactions
(
    id              uuid           default gen_random_uuid()  not null primary key,
    user_id         uuid                                      not null references users(id),
    account_id      uuid                                      not null references accounts(id),
    category_id     uuid                                      not null references categories(id),
    amount          numeric(18, 2)                            not null
        constraint recurring_transactions_amount_check check (amount > 0),
    direction_type  varchar(10)                              not null references direction_types(type),
    day_of_month    smallint                                  not null
        constraint recurring_day_check check (day_of_month between 1 and 31),
    description     varchar(255),
    is_active       boolean        default true               not null,
    created_at      timestamptz    default now()              not null,
    currency_code   char(3)                                   not null references currencies(code),
    last_executed_at timestamptz
);

create index idx_recurring_user   on recurring_transactions (user_id);
create index idx_recurring_active on recurring_transactions (is_active, day_of_month);

create table rm_operations
(
    id              uuid                                    not null primary key,
    user_id         uuid                                    not null references users(id),
    type            varchar(20)                             not null constraint chk_rm_operations_type check (type IN ('Transaction', 'Transfer')),
    occurred_at     timestamptz                             not null,
    description     text                                    null,
    payload         jsonb                                   not null
);

create index idx_rm_operations_cursor on rm_operations (user_id, occurred_at desc, id desc);

create index idx_rm_operations_direction on rm_operations ((payload->>'direction')) where type = 'Transaction';

create table idempotent_commands (
    idempotency_key uuid                                    not null primary key,
    command_type    varchar(100)                            not null,
    response_json   jsonb                                   not null,
    created_at      timestamptz                             not null,
    expires_at      timestamptz                             not null
);

create index ix_idempotent_commands_expires_at on idempotent_commands (expires_at);

create extension if not exists btree_gist;

alter table budgets add constraint uq_budgets_no_overlap exclude using gist (
    user_id with =,
    category_id with =,
    daterange(date_from, date_to, '[]') with &&
);