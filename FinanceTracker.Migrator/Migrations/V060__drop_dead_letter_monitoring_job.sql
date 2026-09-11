DELETE FROM qrtz_triggers
WHERE sched_name = 'DeadLetterMonitorScheduler' AND job_name = 'DeadLetterMonitoringJob';

DELETE FROM qrtz_job_details
WHERE sched_name = 'DeadLetterMonitorScheduler' AND job_name = 'DeadLetterMonitoringJob';
