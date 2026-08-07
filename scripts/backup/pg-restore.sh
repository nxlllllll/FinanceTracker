#!/bin/sh
set -eu

# Restores a dump produced by pg-backup.sh.
#
#   Usage: pg-restore.sh <dump-file> [target-database]

if [ $# -lt 1 ]; then
	echo "Usage: $0 <dump-file> [target-database]" >&2
	exit 1
fi

dump_file="$1"
: "${POSTGRES_HOST:=postgres}"
: "${POSTGRES_PORT:=5432}"
target_db="${2:-${POSTGRES_DB}}"

if [ ! -f "${dump_file}" ]; then
	echo "[restore] ${dump_file} does not exist" >&2
	exit 1
fi

echo "[restore] Checking the archive is readable before touching anything"
pg_restore --list "${dump_file}" > /dev/null

echo "[restore] Restoring ${dump_file} into ${target_db} on ${POSTGRES_HOST}:${POSTGRES_PORT}"
echo "[restore] Stop the API and every worker first. Restoring under live writes gives you a"
echo "[restore] database that is neither the backup nor what was there before."
printf "[restore] Type the database name to confirm: "
read -r confirmation

if [ "${confirmation}" != "${target_db}" ]; then
	echo "[restore] Aborted" >&2
	exit 1
fi

# --clean --if-exists drops the existing objects first. Restoring over a populated database without
# it produces a mix of old and new rows that looks like a successful restore.
# --single-transaction so a failure halfway leaves the database as it was rather than half-restored.
pg_restore \
	--host="${POSTGRES_HOST}" \
	--port="${POSTGRES_PORT}" \
	--username="${POSTGRES_USER}" \
	--dbname="${target_db}" \
	--clean \
	--if-exists \
	--single-transaction \
	--no-owner \
	"${dump_file}"

echo "[restore] Done. Before starting the application back up:"
echo "[restore]   - check the schema_versions table matches the migrator's expectation"
echo "[restore]   - expect the outbox to republish anything unprocessed at dump time"
echo "[restore]   - expect category totals and account balances to be as of the dump, not now"
echo
echo "[restore] Drill: restore into a scratch database instead of the live one —"
echo "[restore]   $0 <dump-file> ft_restore_drill"
echo "[restore] then compare a few row counts. That is the whole exercise, and it is worth"
echo "[restore] doing on a calm day rather than discovering the gap on a bad one."
