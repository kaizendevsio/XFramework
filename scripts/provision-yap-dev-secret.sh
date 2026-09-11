#!/usr/bin/env bash
# Executed by the normal dev deployment as the protected environment owner.
# No credentials are printed. Existing credentials are never rotated implicitly.
set -euo pipefail
target=/opt/xframework/xeon-dev.env
test -f "$target"
test ! -L "$target"
test "$(stat -Lc '%u:%a:%h' "$target")" = "$(id -u):600:1"
exec 9>/opt/xframework/.yap-service-secret.lock
flock 9
umask 077
if ! grep -q '^YAP_SERVICE_IDENTITY_SECRET=' "$target"; then
    printf '\nYAP_SERVICE_IDENTITY_SECRET=%s\n' "$(openssl rand -hex 48)" >> "$target"
fi
if ! grep -q '^YAP_SERVICE_CREDENTIAL_GENERATION_ID=' "$target"; then
    printf '\nYAP_SERVICE_CREDENTIAL_GENERATION_ID=yap-dev-%s\n' "$(openssl rand -hex 8)" >> "$target"
fi
# Protected configuration handoff for the separately hosted Yap development app.
install -d -m 700 /opt/xframework/client-config
python3 - "$target" /opt/xframework/client-config/yap.service-identity.json <<'PY'
import json
import os
import pathlib
import re
import sys

values = dict(line.split('=', 1) for line in pathlib.Path(sys.argv[1]).read_text().splitlines()
              if line.startswith(('YAP_SERVICE_IDENTITY_SECRET=', 'YAP_SERVICE_CREDENTIAL_GENERATION_ID=')))
secret = values['YAP_SERVICE_IDENTITY_SECRET']
generation = values['YAP_SERVICE_CREDENTIAL_GENERATION_ID']
if len(secret) < 64 or not re.fullmatch(r'[A-Za-z0-9_.:-]{1,96}', generation):
    raise SystemExit('Yap service identity configuration is invalid')
path = pathlib.Path(sys.argv[2])
if path.is_symlink():
    raise SystemExit('Yap handoff must not be a symbolic link')
with open(path, 'w', encoding='utf-8') as output:
    json.dump({'ServiceIdentity': {'ClientId': 'XFramework.Yap', 'ClientSecret': secret,
                                  'GenerationId': generation}}, output, indent=2)
os.chmod(path, 0o600)
PY
echo 'Dedicated Yap service identity is provisioned; protected handoff is ready.'
