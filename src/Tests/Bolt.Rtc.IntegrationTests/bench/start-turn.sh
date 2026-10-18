#!/usr/bin/env bash
set -euo pipefail
docker run --detach --name coturn --network host \
  coturn/coturn:4.18.0-debian@sha256:bbefd3e1fdfdc0d58770fe01b581fd8b00d9f3a5580d00acb77cf719a6bc78e3 \
  -n --log-file=stdout --listening-ip=127.0.0.2 --listening-ip=127.0.0.3 --listening-ip=127.0.0.4 --relay-ip=127.0.0.1 --listening-port=3478 \
  --use-auth-secret --static-auth-secret=ci-turn-secret --realm=bolt.test --fingerprint \
  --no-tls --no-tcp-relay --allow-loopback-peers --min-port=49160 --max-port=49400
for attempt in $(seq 1 30); do
  if ss -lnu | grep -q '127.0.0.4:3478 '; then exit 0; fi
  sleep 1
done
docker logs coturn
exit 1
