create table qrtz_job_details
(
    sched_name text not null,
    job_name text not null,
    job_group text not null,
    description text null,
    job_class_name text not null,
    is_durable bool not null,
    is_nonconcurrent bool not null,
    is_update_data bool not null,
    requests_recovery bool not null,
    job_data bytea null,
    primary key (sched_name, job_name, job_group)
);

create table qrtz_triggers
(
    sched_name text not null,
    trigger_name text not null,
    trigger_group text not null,
    job_name text not null,
    job_group text not null,
    description text null,
    next_fire_time bigint null,
    prev_fire_time bigint null,
    priority integer null,
    trigger_state text not null,
    trigger_type text not null,
    start_time bigint not null,
    end_time bigint null,
    calendar_name text null,
    misfire_instr smallint null,
    misfire_orig_fire_time bigint null,
    job_data bytea null,
    primary key (sched_name, trigger_name, trigger_group),
    foreign key (sched_name, job_name, job_group) references qrtz_job_details (sched_name, job_name, job_group)
);

create table qrtz_simple_triggers
(
    sched_name text not null,
    trigger_name text not null,
    trigger_group text not null,
    repeat_count bigint not null,
    repeat_interval bigint not null,
    times_triggered bigint not null,
    primary key (sched_name, trigger_name, trigger_group),
    foreign key (sched_name, trigger_name, trigger_group)
        references qrtz_triggers (sched_name, trigger_name, trigger_group)
        on delete cascade
);

create table qrtz_simprop_triggers
(
    sched_name text not null,
    trigger_name text not null,
    trigger_group text not null,
    str_prop_1 text null,
    str_prop_2 text null,
    str_prop_3 text null,
    int_prop_1 integer null,
    int_prop_2 integer null,
    long_prop_1 bigint null,
    long_prop_2 bigint null,
    dec_prop_1 numeric null,
    dec_prop_2 numeric null,
    bool_prop_1 bool null,
    bool_prop_2 bool null,
    time_zone_id text null,
    primary key (sched_name, trigger_name, trigger_group),
    foreign key (sched_name, trigger_name, trigger_group)
        references qrtz_triggers (sched_name, trigger_name, trigger_group)
        on delete cascade
);

create table qrtz_cron_triggers
(
    sched_name text not null,
    trigger_name text not null,
    trigger_group text not null,
    cron_expression text not null,
    time_zone_id text,
    primary key (sched_name, trigger_name, trigger_group),
    foreign key (sched_name, trigger_name, trigger_group)
        references qrtz_triggers (sched_name, trigger_name, trigger_group)
        on delete cascade
);

create table qrtz_blob_triggers
(
    sched_name text not null,
    trigger_name text not null,
    trigger_group text not null,
    blob_data bytea null,
    primary key (sched_name, trigger_name, trigger_group),
    foreign key (sched_name, trigger_name, trigger_group)
        references qrtz_triggers (sched_name, trigger_name, trigger_group)
        on delete cascade
);

create table qrtz_calendars
(
    sched_name text not null,
    calendar_name text not null,
    calendar bytea not null,
    primary key (sched_name, calendar_name)
);

create table qrtz_paused_trigger_grps
(
    sched_name text not null,
    trigger_group text not null,
    primary key (sched_name, trigger_group)
);

create table qrtz_fired_triggers
(
    sched_name text not null,
    entry_id text not null,
    trigger_name text not null,
    trigger_group text not null,
    instance_name text not null,
    fired_time bigint not null,
    sched_time bigint not null,
    priority integer not null,
    state text not null,
    job_name text null,
    job_group text null,
    is_nonconcurrent bool not null,
    requests_recovery bool null,
    primary key (sched_name, entry_id)
);

create table qrtz_scheduler_state
(
    sched_name text not null,
    instance_name text not null,
    last_checkin_time bigint not null,
    checkin_interval bigint not null,
    primary key (sched_name, instance_name)
);

create table qrtz_locks
(
    sched_name text not null,
    lock_name text not null,
    primary key (sched_name, lock_name)
);

create index idx_qrtz_j_req_recovery on qrtz_job_details (requests_recovery);
create index idx_qrtz_t_next_fire_time on qrtz_triggers (next_fire_time);
create index idx_qrtz_t_state on qrtz_triggers (trigger_state);
create index idx_qrtz_t_nft_st on qrtz_triggers (next_fire_time, trigger_state);
create index idx_qrtz_ft_trig_name on qrtz_fired_triggers (trigger_name);
create index idx_qrtz_ft_trig_group on qrtz_fired_triggers (trigger_group);
create index idx_qrtz_ft_trig_nm_gp on qrtz_fired_triggers (sched_name, trigger_name, trigger_group);
create index idx_qrtz_ft_trig_inst_name on qrtz_fired_triggers (instance_name);
create index idx_qrtz_ft_job_name on qrtz_fired_triggers (job_name);
create index idx_qrtz_ft_job_group on qrtz_fired_triggers (job_group);
create index idx_qrtz_ft_job_req_recovery on qrtz_fired_triggers (requests_recovery);