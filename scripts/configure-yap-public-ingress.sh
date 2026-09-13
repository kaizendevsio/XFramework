#!/usr/bin/env bash
# Opt-in public sharing of Yap only. Existing private Serve listeners are untouched.
set -euo pipefail
tailscale serve status --json | python3 -c '
import json, sys
state = json.load(sys.stdin)
listener = state.get("TCP", {}).get("8443")
if listener is not None and listener != {"HTTPS": True}:
    raise SystemExit("Port 8443 already has a different listener")
for address, site in state.get("Web", {}).items():
    if address.endswith(":8443") and site.get("Handlers") != {"/": {"Proxy": "http://127.0.0.1:5188"}}:
        raise SystemExit("Port 8443 already serves another application")
'
tailscale funnel --bg --yes --https=8443 http://127.0.0.1:5188
curl --fail --silent --show-error --connect-timeout 5 --max-time 15 \
    https://xeon-dev.tailed40e.ts.net:8443/health/ready
