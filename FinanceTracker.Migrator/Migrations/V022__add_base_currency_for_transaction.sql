alter table rm_transactions add column base_currency_code char(3) not null default 'RUB';

update rm_transactions t
set base_currency_code = a.currency_code
from accounts a
where a.id = t.account_id;

alter table rm_transactions alter column base_currency_code drop default;