#!/bin/bash
set -euo pipefail
if [[ $# -ne 2 ]]; then
  echo "Usage: $0 <device-id> <private-invitation-file>" >&2
  exit 2
fi
# Run on the paired Mac. Do not enable shell tracing: invitations are credentials.
# The app consumes this once; the normal TLS pin and pairing secret still apply.
invitation=$(cat "$2")
if [[ -z "$invitation" || ${#invitation} -gt 4096 ]]; then
  echo "Invalid invitation file." >&2
  exit 2
fi
DEVICECTL_CHILD_YAP_BENCHMARK_INVITATION="$invitation" \
  xcrun devicectl device process launch --device "$1" --terminate-existing \
  io.kaizendevs.yap.nativebenchmark.personal
