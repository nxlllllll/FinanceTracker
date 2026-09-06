set -euo pipefail

TIMEOUT=${SMOKE_TIMEOUT_SECONDS:-120}

SERVICES=(
	"api:9100"
	"worker-outbox:5000"
	"worker-account-projection:5001"
	"worker-recurring-transaction:5002"
	"worker-recurring-transaction-projection:5003"
	"worker-dead-letter-monitor:5004"
	"worker-transfer-projection:5005"
	"worker-currency-rate:5006"
	"worker-balance-adjustment:5007"
	"worker-cleanup:5008"
	"worker-permission-projection:5009"
	"worker-user-role-projection:5010"
	"worker-base-currency-recalculation:5011"
)

failed=0

for entry in "${SERVICES[@]}"; do
	name=${entry%%:*}
	port=${entry##*:}
	url="http://127.0.0.1:${port}/health/ready"
	deadline=$(( SECONDS + TIMEOUT ))

	until curl -fsS -o /dev/null --max-time 5 "$url"; do
		if (( SECONDS >= deadline )); then
			echo "::error::${name} did not answer ready within ${TIMEOUT}s"
			curl -sS --max-time 5 "$url" || true
			failed=1
			continue 2
		fi
		sleep 3
	done

	echo "ready: ${name}"
done

status=$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 http://127.0.0.1:8080/api/v1/categories || true)

if [[ "$status" != "401" ]]; then
	echo "::error::the public port answered ${status} for an unauthenticated request, expected 401"
	failed=1
fi

exit $failed
