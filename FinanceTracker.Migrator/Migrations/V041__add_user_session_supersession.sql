alter table user_sessions
    add column superseded_by_session_id uuid;
