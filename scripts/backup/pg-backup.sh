#!/bin/sh
set -eu

# Dumps the database to a rotating set of local files.

: "${POSTGRES_HOST:=postgres}"
: "${POSTGRES_PORT:=5432}"
: "${METRICS_DIR:=/metrics}"
: "${BACKUP_DIR:=/backups}"
: "${BACKUP_KEEP:=14}"

timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
target="${BACKUP_DIR}/${POSTGRES_DB}_${timestamp}.dump"

echo "[backup] Dumping ${POSTGRES_DB} from ${POSTGRES_HOST}:${POSTGRES_PORT} to ${target}"

# -Fc: custom format, so pg_restore can pick out individual tables during a partial recovery.
pg_dump \
	--host="${POSTGRES_HOST}" \
	--port="${POSTGRES_PORT}" \
	--username="${POSTGRES_USER}" \
	--dbname="${POSTGRES_DB}" \
	--format=custom \
	--compress=6 \
	--file="${target}.partial"

mv "${target}.partial" "${target}"

# Verifying the archive is readable is the cheap half of a restore drill.
# It catches a corrupt or truncated dump now
pg_restore --list "${target}" > /dev/null
echo "[backup] Wrote and verified $(du -h "${target}" | cut -f1)"

find "${BACKUP_DIR}" -maxdepth 1 -name "${POSTGRES_DB}_*.dump" -type f \
	| sort -r \
	| tail -n "+$((BACKUP_KEEP + 1))" \
	| while read -r old; do
		echo "[backup] Removing ${old}"
		rm -f "${old}"
	done

if [ -d "${METRICS_DIR}" ]; then
	cat > "${METRICS_DIR}/pg_backup.prom.tmp" <<EOF
pg_backup_last_success_timestamp_seconds $(date -u +%s)
pg_backup_size_bytes $(wc -c < "${target}")
EOF
	mv "${METRICS_DIR}/pg_backup.prom.tmp" "${METRICS_DIR}/pg_backup.prom"
fi

echo "[backup] Done"
