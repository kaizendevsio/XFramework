#!/usr/bin/env bash
# Own only Portal scanner HTTPS 5443. Never reset Serve or change Funnel.
set -euo pipefail
umask 077
test "$#" -eq 2
case "$1" in configure|rollback) ;; *) exit 1 ;; esac
if [ "$1" = rollback ] && [ ! -f "$2" ]; then exit 0; fi

decision="$(tailscale serve status --json | python3 -c '
import json, pathlib, sys

mode, marker_name = sys.argv[1:]
marker = pathlib.Path(marker_name)
state = json.load(sys.stdin)
address = "xeon-dev.tailed40e.ts.net:5443"
expected = {"Handlers": {"/": {"Proxy": "http://127.0.0.1:5000"}}}

def owns_port(config):
    return ("5443" in config.get("TCP", {})
            or any(key.endswith(":5443") for key in config.get("Web", {}))
            or any(key.endswith(":5443") and value
                   for key, value in config.get("AllowFunnel", {}).items()))

if any(owns_port(config) for config in state.get("Foreground", {}).values()):
    raise SystemExit("Port 5443 has a foreground Serve owner")
if any(enabled for key, enabled in state.get("AllowFunnel", {}).items() if key.endswith(":5443")):
    raise SystemExit("Portal scanner ingress must remain private to the tailnet")
listener = state.get("TCP", {}).get("5443")
sites = {key: site for key, site in state.get("Web", {}).items() if key.endswith(":5443")}
absent = listener is None and not sites
matching = listener == {"HTTPS": True} and sites == {address: expected}
if not absent and not matching:
    raise SystemExit("Port 5443 already has a different Serve listener or route")

if mode == "configure":
    # Record ownership before mutation so readiness failure is still recoverable.
    with marker.open("x", encoding="utf-8") as stream:
        json.dump({"created": absent}, stream)
    print("create" if absent else "reuse")
else:
    ownership = json.loads(marker.read_text(encoding="utf-8"))
    if not isinstance(ownership, dict) or set(ownership) != {"created"} or type(ownership["created"]) is not bool:
        raise SystemExit("Invalid scanner ingress ownership marker")
    print("remove" if ownership["created"] and matching else "preserve")
' "$1" "$2")"

if [ "$1" = rollback ]; then
    if [ "$decision" = remove ]; then
        tailscale serve --bg --yes --https=5443 off
    fi
    rm -f -- "$2"
    exit 0
fi

if [ "$decision" = create ]; then
    tailscale serve --bg --yes --https=5443 http://127.0.0.1:5000
fi
base=https://xeon-dev.tailed40e.ts.net:5443
curl --fail --silent --show-error --connect-timeout 5 --max-time 15 \
    --retry 5 --retry-delay 2 --retry-connrefused "$base/health/ready"
curl --fail --silent --show-error --connect-timeout 5 --max-time 15 \
    --output /dev/null "$base/_framework/blazor.web.js"
