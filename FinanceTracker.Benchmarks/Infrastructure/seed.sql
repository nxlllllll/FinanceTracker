-- 1000 пользователей — реалистичная мультитенантность
INSERT INTO users (id, email, password_hash, base_currency_code)
SELECT
    CASE WHEN i = 1 THEN '{UserId}'::uuid ELSE gen_random_uuid() END,
    'user' || i || '@bench.com',
    'hash',
    CASE WHEN i % 3 = 0 THEN 'USD' WHEN i % 3 = 1 THEN 'EUR' ELSE 'RUB' END
FROM generate_series(1, 1000) i
ON CONFLICT DO NOTHING;

-- 5 счетов на пользователя, только допустимые типы: checking / savings / cash
INSERT INTO accounts (id, user_id, name, account_type_code, currency_code)
SELECT
    CASE
        WHEN u.rn = 1 AND a.i = 1 THEN '{AccountId}'::uuid
        WHEN u.rn = 1 AND a.i = 2 THEN '{FromAccountId}'::uuid
        WHEN u.rn = 1 AND a.i = 3 THEN '{ToAccountId}'::uuid
        ELSE gen_random_uuid()
        END,
    u.id,
    'Account ' || a.i,
    CASE WHEN a.i % 3 = 0 THEN 'savings' WHEN a.i % 3 = 1 THEN 'cash' ELSE 'checking' END,
    CASE WHEN a.i % 3 = 0 THEN 'USD' WHEN a.i % 3 = 1 THEN 'EUR' ELSE 'RUB' END
FROM (SELECT id, ROW_NUMBER() OVER () AS rn FROM users) u
         CROSS JOIN generate_series(1, 5) a(i);

INSERT INTO rm_account_balances (account_id, balance, last_version, updated_at)
SELECT id, (random() * 500000 + 1000)::numeric(18,2), 1, now()
FROM accounts;

-- 20 категорий на пользователя, income + expense
INSERT INTO categories (id, user_id, name, type_code)
SELECT
    CASE
        WHEN u.rn = 1 AND c.i = 1 THEN '{CategoryId}'::uuid
        WHEN u.rn = 1 AND c.i = 2 THEN '{ExpenseCategoryId}'::uuid
        ELSE gen_random_uuid()
        END,
    u.id,
    'Category ' || c.i,
    CASE WHEN c.i % 2 = 0 THEN 'expense' ELSE 'income' END
FROM (SELECT id, ROW_NUMBER() OVER () AS rn FROM users) u
         CROSS JOIN generate_series(1, 20) c(i);

-- Бюджеты для целевого пользователя
INSERT INTO budgets (id, user_id, category_id, amount, currency_code, date_from, date_to)
SELECT
    CASE WHEN ROW_NUMBER() OVER () = 1 THEN '{BudgetId}'::uuid ELSE gen_random_uuid() END,
    '{UserId}'::uuid,
    id,
    (random() * 50000 + 5000)::numeric(18,2),
    'RUB',
    date_trunc('month', now())::date,
    (date_trunc('month', now()) + interval '1 month - 1 day')::date
FROM categories
WHERE user_id = '{UserId}'::uuid
LIMIT 10;

INSERT INTO rm_budget_progress (budget_id, spent, updated_at)
SELECT id, (random() * 10000)::numeric(18,2), now() FROM budgets;

-- Транзакции: размазаны по всем пользователям и их аккаунтам
-- ~0.5% с is_rate_pending = true (кросс-валютные без курса)
INSERT INTO rm_transactions (id, account_id, user_id, category_id, amount, currency_code, base_currency_code, direction_type, exchange_rate, is_excluded, description, is_rate_pending, occurred_at)
SELECT
    gen_random_uuid(),
    a.id,
    a.user_id,
    c.id,
    (random() * 9900 + 100)::numeric(18,2),
    a.currency_code,
    a.currency_code,
    CASE WHEN i % 3 = 0 THEN 'credit' ELSE 'debit' END,
    CASE WHEN a.currency_code != 'RUB' THEN (0.8 + random() * 0.4)::numeric(18,6) ELSE 1.0 END,
    (random() < 0.05),
    'Tx ' || i,
    (random() < 0.005 AND a.currency_code != 'RUB'),
    now() - (random() * interval '365 days')
FROM generate_series(1, 1000000) i
         JOIN LATERAL (
    SELECT ac.id, ac.user_id, ac.currency_code
    FROM accounts ac
    ORDER BY random()
    LIMIT 1
    ) a ON true
         JOIN LATERAL (
    SELECT id FROM categories WHERE user_id = a.user_id ORDER BY random() LIMIT 1
    ) c ON true;

-- Реалистичный объём для целевого пользователя: ~2000 транзакций за год
INSERT INTO rm_transactions (id, account_id, user_id, category_id, amount, currency_code, base_currency_code, direction_type, exchange_rate, is_excluded, description, is_rate_pending, occurred_at)
SELECT
    gen_random_uuid(),
    CASE WHEN i % 3 = 0 THEN '{AccountId}'::uuid ELSE '{FromAccountId}'::uuid END,
    '{UserId}'::uuid,
    CASE WHEN i % 2 = 0 THEN '{CategoryId}'::uuid ELSE '{ExpenseCategoryId}'::uuid END,
    (random() * 9900 + 100)::numeric(18,2),
    'RUB',
    'RUB',
    CASE WHEN i % 3 = 0 THEN 'credit' ELSE 'debit' END,
    1.0,
    (random() < 0.03),
    'My tx ' || i,
    false,
    now() - (random() * interval '365 days')
FROM generate_series(1, 2000) i;

-- Трансферы: размазаны по пользователям
-- 1% pending_credit, 2% compensated, ~1.3% failed, остальные completed
INSERT INTO rm_transfers (id, user_id, from_account_id, to_account_id, amount_from, currency_from, amount_to, currency_to, exchange_rate, description, is_rate_pending, status, occurred_at)
SELECT
    gen_random_uuid(),
    a1.user_id,
    a1.id,
    a2.id,
    (random() * 9900 + 100)::numeric(18,2),
    a1.currency_code,
    (random() * 9900 + 100)::numeric(18,2),
    a2.currency_code,
    CASE WHEN a1.currency_code != a2.currency_code THEN (0.8 + random() * 0.4)::numeric(18,6) ELSE 1.0 END,
    'Transfer ' || i,
    false,
    CASE
        WHEN i % 100 = 0 THEN 'pending_credit'
        WHEN i % 50  = 0 THEN 'compensated'
        WHEN i % 75  = 0 THEN 'failed'
        ELSE 'completed'
        END,
    now() - (random() * interval '365 days')
FROM generate_series(1, 500000) i
         JOIN LATERAL (
    SELECT ac.id, ac.user_id, ac.currency_code
    FROM accounts ac
    ORDER BY random()
    LIMIT 1
    ) a1 ON true
         JOIN LATERAL (
    SELECT ac.id, ac.currency_code
    FROM accounts ac
    WHERE ac.user_id = a1.user_id AND ac.id != a1.id
    ORDER BY random()
    LIMIT 1
    ) a2 ON true;

-- Трансферы целевого пользователя с именованными счетами (~500 штук)
INSERT INTO rm_transfers (id, user_id, from_account_id, to_account_id, amount_from, currency_from, amount_to, currency_to, exchange_rate, description, is_rate_pending, status, occurred_at)
SELECT
    CASE WHEN i = 1 THEN '{TransferId}'::uuid ELSE gen_random_uuid() END,
    '{UserId}'::uuid,
    '{FromAccountId}'::uuid,
    '{ToAccountId}'::uuid,
    (random() * 9900 + 100)::numeric(18,2),
    'RUB',
    (random() * 9900 + 100)::numeric(18,2),
    'RUB',
    1.0,
    'My transfer ' || i,
    false,
    CASE WHEN i % 100 = 0 THEN 'pending_credit' ELSE 'completed' END,
    now() - (random() * interval '365 days')
FROM generate_series(1, 500) i;

-- Category totals
INSERT INTO rm_category_totals (id, user_id, category_id, period, total, transaction_count, updated_at)
SELECT
    gen_random_uuid(),
    '{UserId}'::uuid,
    CASE WHEN i % 2 = 0 THEN '{CategoryId}'::uuid ELSE '{ExpenseCategoryId}'::uuid END,
    date_trunc('month', now() - (i || ' months')::interval)::date,
    (random() * 100000)::numeric(18,2),
    (random() * 200 + 10)::int,
    now()
FROM generate_series(0, 23) i;

-- Курсы валют за 2 года
INSERT INTO currency_rates (base_code, target_code, rate, actual_at)
SELECT 'USD', 'RUB', (85 + random() * 10)::numeric(18,6), (now() - (i || ' days')::interval)::date
FROM generate_series(0, 730) i
ON CONFLICT DO NOTHING;

INSERT INTO currency_rates (base_code, target_code, rate, actual_at)
SELECT 'EUR', 'RUB', (90 + random() * 10)::numeric(18,6), (now() - (i || ' days')::interval)::date
FROM generate_series(0, 730) i
ON CONFLICT DO NOTHING;

INSERT INTO currency_rates (base_code, target_code, rate, actual_at)
SELECT 'USD', 'EUR', (0.9 + random() * 0.05)::numeric(18,6), (now() - (i || ' days')::interval)::date
FROM generate_series(0, 730) i
ON CONFLICT DO NOTHING;

-- Recurring transactions: 200k записей размазаны по пользователям
-- 90% активных, 80% уже выполнены в текущем месяце
INSERT INTO recurring_transactions (id, user_id, account_id, category_id, amount, direction_type, day_of_month, description, currency_code, is_active, last_executed_at)
SELECT
    gen_random_uuid(),
    a.user_id,
    a.id,
    c.id,
    (random() * 9900 + 100)::numeric(18,2),
    CASE WHEN i % 3 = 0 THEN 'credit' ELSE 'debit' END,
    (i % 28 + 1)::smallint,
    'Recurring ' || i,
    a.currency_code,
    (random() > 0.1),
    CASE
        WHEN random() < 0.80 THEN '{CurrentMonthStart}'::timestamptz
        WHEN random() < 0.10 THEN now() - interval '1 month'
        ELSE NULL
        END
FROM generate_series(1, 200000) i
         JOIN LATERAL (
    SELECT ac.id, ac.user_id, ac.currency_code
    FROM accounts ac
    ORDER BY random()
    LIMIT 1
    ) a ON true
         JOIN LATERAL (
    SELECT id FROM categories WHERE user_id = a.user_id ORDER BY random() LIMIT 1
    ) c ON true;

-- Сессия целевого пользователя
INSERT INTO user_sessions (id, user_id, refresh_token_hash, expires_at, created_at)
VALUES (gen_random_uuid(), '{UserId}'::uuid, '{RefreshTokenHash}', now() + interval '7 days', now())
ON CONFLICT DO NOTHING;

-- Дополнительные сессии для нагрузки на индекс
INSERT INTO user_sessions (id, user_id, refresh_token_hash, expires_at, created_at)
SELECT
    gen_random_uuid(),
    u.id,
    md5(random()::text || clock_timestamp()::text),
    now() + interval '7 days',
    now()
FROM generate_series(1, 5000) i
         JOIN LATERAL (SELECT id FROM users ORDER BY random() LIMIT 1) u ON true;