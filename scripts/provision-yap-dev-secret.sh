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
# Preserve configured workspaces; initialize this dev host with the provisioned Yap workspace.
if ! grep -q '^YAP_TENANT_ID=' "$target"; then
    printf '\nYAP_TENANT_ID=c4af50e9-325c-4b17-a4e9-ada54c9475b9\n' >> "$target"
fi
if ! grep -q '^YAP_ROLE_ID=' "$target"; then
    printf '\nYAP_ROLE_ID=633a467c-53a8-4c51-8f19-f655d5391f22\n' >> "$target"
fi
# RFC 8292 application server keys for web push. Generated once on this host and never
# rotated implicitly: new keys silently invalidate every push subscription already stored
# on every device, and each one only recovers when that browser resubscribes.
if ! grep -q '^WEB_PUSH_VAPID_PRIVATE_KEY=' "$target"; then
    python3 - "$target" <<'PY'
import base64
import os
import re
import subprocess
import sys
import tempfile

with tempfile.TemporaryDirectory() as work:
    pem = os.path.join(work, 'vapid.pem')
    subprocess.run(['openssl', 'ecparam', '-name', 'prime256v1', '-genkey', '-noout', '-out', pem], check=True)
    os.chmod(pem, 0o600)
    text = subprocess.run(['openssl', 'ec', '-in', pem, '-text', '-noout'],
                          check=True, capture_output=True, text=True).stdout

def field(name):
    match = re.search(r'^' + name + r':\n((?:\s+[0-9a-f:]+\n)+)', text, re.MULTILINE)
    if not match:
        raise SystemExit('openssl did not report the ' + name + ' key')
    return bytes.fromhex(''.join(match.group(1).split()).replace(':', ''))

# openssl pads the scalar with a leading zero byte whenever the high bit is set.
private, public = field('priv')[-32:], field('pub')
if len(private) != 32 or len(public) != 65 or public[0] != 0x04:
    raise SystemExit('Generated VAPID key pair has an unexpected shape')

def encode(raw):
    return base64.urlsafe_b64encode(raw).decode().rstrip('=')

with open(sys.argv[1], 'a', encoding='utf-8') as env:
    env.write('\nWEB_PUSH_VAPID_PUBLIC_KEY=' + encode(public) + '\n')
    env.write('WEB_PUSH_VAPID_PRIVATE_KEY=' + encode(private) + '\n')
PY
fi
# Contact of record for the push services. Deliberately not a personal address.
if ! grep -q '^WEB_PUSH_VAPID_SUBJECT=' "$target"; then
    printf '\nWEB_PUSH_VAPID_SUBJECT=%s\n' 'https://xeon-dev.tailed40e.ts.net:5188' >> "$target"
fi
# Protected configuration handoff for local Yap development.
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
