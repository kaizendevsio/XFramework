#!/usr/bin/env bash
# Own only Yap's tailnet HTTPS listener. Never reset other Serve routes or enable Funnel.
set -euo pipefail
test "$#" -eq 1

tailscale serve status --json | python3 -c '
import json, sys
state = json.load(sys.stdin)
listener = state.get("TCP", {}).get("5188")
if listener is not None and listener != {"HTTPS": True}:
    raise SystemExit("Port 5188 already has a different Serve listener")
for address, site in state.get("Web", {}).items():
    if address.endswith(":5188") and site.get("Handlers") != {"/": {"Proxy": "http://127.0.0.1:5188"}}:
        raise SystemExit("Port 5188 already has a different Serve route")
if any(enabled for address, enabled in state.get("AllowFunnel", {}).items() if address.endswith(":5188")):
    raise SystemExit("Yap must remain private to the tailnet")
'

# Rollback may remove this listener only after its ownership check succeeded.
touch "$1"
tailscale serve --bg --yes --https=5188 http://127.0.0.1:5188
curl --fail --silent --show-error --connect-timeout 5 --max-time 15 \
    --retry 5 --retry-delay 2 --retry-connrefused \
    https://xeon-dev.tailed40e.ts.net:5188/health/ready
