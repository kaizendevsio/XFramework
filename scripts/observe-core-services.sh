#!/usr/bin/env bash
# Run concurrently with the authenticated smoke; both must pass before activation.
set -euo pipefail
[[ "$OBSERVATION_SECONDS" =~ ^[1-9][0-9]{1,3}$ ]]
deadline=$((SECONDS + OBSERVATION_SECONDS))
samples=0
while [ "$SECONDS" -lt "$deadline" ]; do
  curl -fsS --connect-timeout 3 --max-time 10 "$IDENTITY_PUBLIC_URL/health/live" >/dev/null
  curl -fsS --connect-timeout 3 --max-time 10 "$HUB_PUBLIC_URL/health/ready" >/dev/null
  curl -fsS --connect-timeout 3 --max-time 10 http://127.0.0.1:5148/health/ready >/dev/null
  samples=$((samples + 1))
  remaining=$((deadline - SECONDS))
  [ "$remaining" -le 0 ] && break
  [ "$remaining" -lt 15 ] && sleep "$remaining" || sleep 15
done
test "$samples" -ge 4
echo "Core observation passed with ${samples} samples."
