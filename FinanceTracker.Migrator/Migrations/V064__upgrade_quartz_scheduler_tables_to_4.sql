ALTER TABLE qrtz_triggers
	ADD COLUMN execution_group VARCHAR(200) NULL,
	ADD COLUMN preferred_node VARCHAR(200) NULL,
	ADD COLUMN preferred_node_auto BOOL NOT NULL DEFAULT FALSE,
	ADD COLUMN retry_policy VARCHAR(250) NULL,
	ADD COLUMN retry_attempt INTEGER NULL;

ALTER TABLE qrtz_fired_triggers
	ADD COLUMN execution_group VARCHAR(200) NULL;

CREATE TABLE qrtz_paused_job_grps
(
	sched_name TEXT NOT NULL,
	job_group TEXT NOT NULL,
	PRIMARY KEY (sched_name, job_group)
);

DROP INDEX idx_qrtz_j_req_recovery;
DROP INDEX idx_qrtz_t_next_fire_time;
DROP INDEX idx_qrtz_t_state;
DROP INDEX idx_qrtz_t_nft_st;
DROP INDEX idx_qrtz_ft_trig_name;
DROP INDEX idx_qrtz_ft_trig_group;
DROP INDEX idx_qrtz_ft_trig_nm_gp;
DROP INDEX idx_qrtz_ft_trig_inst_name;
DROP INDEX idx_qrtz_ft_job_name;
DROP INDEX idx_qrtz_ft_job_group;
DROP INDEX idx_qrtz_ft_job_req_recovery;

CREATE INDEX idx_qrtz_j_g_n ON qrtz_job_details (sched_name, job_group, job_name);
CREATE INDEX idx_qrtz_t_j ON qrtz_triggers (sched_name, job_name, job_group);
CREATE INDEX idx_qrtz_t_c ON qrtz_triggers (sched_name, calendar_name);
CREATE INDEX idx_qrtz_t_g_n ON qrtz_triggers (sched_name, trigger_group, trigger_name);
CREATE INDEX idx_qrtz_t_nft_st ON qrtz_triggers (sched_name, trigger_state, next_fire_time ASC, priority DESC, misfire_instr);
CREATE INDEX idx_qrtz_ft_inst_job_req_rcvry ON qrtz_fired_triggers (sched_name, instance_name, requests_recovery);
CREATE INDEX idx_qrtz_ft_j_g ON qrtz_fired_triggers (sched_name, job_name, job_group);
CREATE INDEX idx_qrtz_ft_t_g ON qrtz_fired_triggers (sched_name, trigger_name, trigger_group);
