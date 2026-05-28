CREATE TABLE transfer_statuses (
    code        varchar(20) primary key ,
    description varchar(100) not null
);

insert into transfer_statuses(code, description) values
    ('pending_credit', 'Debit applied, waiting for credit'),
    ('completed',      'Both debit and credit applied'),
    ('compensated',    'Debit refunded due to failed credit'),
    ('failed',         'Transfer failed, requires manual resolution');

alter table rm_transfers add column status varchar(20) not null default 'pending_credit' references transfer_statuses(code);

create index ix_transfers_pending_credit on rm_transfers (status, occurred_at) where status = 'pending_credit';