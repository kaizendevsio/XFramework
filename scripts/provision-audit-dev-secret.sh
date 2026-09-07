#!/usr/bin/env bash
# Run as the protected env owner (github-runner) on xeon-dev. Never prints the credential.
set -euo pipefail
target=/opt/xframework/xeon-dev.env
test -f "$target"
test ! -L "$target"
exec 9>/opt/xframework/.audit-reader-secret.lock
flock 9
if ! grep -q '^AUDIT_SERVICE_IDENTITY_SECRET=' "$target"; then
    umask 077
    printf '\nAUDIT_SERVICE_IDENTITY_SECRET=%s\n' "$(openssl rand -hex 48)" >> "$target"
fi
echo 'Audit service credential is provisioned.'
