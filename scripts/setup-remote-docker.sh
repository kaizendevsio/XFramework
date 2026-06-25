#!/bin/bash
# =============================================================================
# XFramework Remote Docker Host Setup
# Run this on the Ubuntu VM: sudo bash setup-remote-docker.sh
# =============================================================================
set -euo pipefail

echo "=== XFramework Docker Host Setup ==="
echo "Ubuntu $(lsb_release -rs) on $(hostname)"
echo ""

# 1. Install Docker Engine (official repo)
echo "[1/5] Installing Docker Engine..."
apt-get update -qq
apt-get install -y -qq ca-certificates curl gnupg

install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc

echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] \
https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "${UBUNTU_CODENAME:-$VERSION_CODENAME}") stable" \
> /etc/apt/sources.list.d/docker.list

apt-get update -qq
apt-get install -y -qq docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# 2. Add user to docker group (rootless access)
echo "[2/5] Configuring user access..."
usermod -aG docker xeon

# 3. Enable Docker service
echo "[3/5] Enabling Docker service..."
systemctl enable --now docker
systemctl enable --now containerd

# 4. Configure Docker to listen on TCP (for remote context) and SSH
echo "[4/5] Configuring Docker remote access..."

# SSH-based access is preferred (secure, no extra ports needed)
# But also enable TCP on localhost for flexibility
mkdir -p /etc/docker
cat > /etc/docker/daemon.json << 'EOF'
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "3"
  },
  "default-address-pools": [
    {"base": "172.17.0.0/12", "size": 24}
  ]
}
EOF

systemctl restart docker

# 5. Firewall — open ports for services
echo "[5/5] Configuring firewall..."
if command -v ufw &> /dev/null; then
    ufw allow OpenSSH
    ufw allow 5432/tcp   comment 'Postgres'
    ufw allow 7000/tcp   comment 'StreamFlow'
    ufw allow 8261/tcp   comment 'IdentityServer'
    ufw allow 5148/tcp   comment 'Messaging'
    ufw allow 5166/tcp   comment 'Notifications'
    ufw allow 5182/tcp   comment 'Attendance'
    ufw allow 5274/tcp   comment 'SmsGateway'
    ufw allow 9696/tcp   comment 'Wallets'
    ufw allow 8105/tcp   comment 'Inventario'
    ufw allow 5000/tcp   comment 'ControlPanel'
    ufw allow 5050/tcp   comment 'OperationsDashboard'
    ufw --force enable
    echo "UFW firewall configured."
else
    echo "UFW not installed — skipping firewall config. Make sure ports are accessible."
fi

# Verify
echo ""
echo "=== Setup Complete ==="
docker --version
docker compose version
echo ""
echo "Docker is running: $(systemctl is-active docker)"
echo ""
echo "Next steps (run from your Windows machine):"
echo "  1. ssh-keygen -t ed25519  (if you don't have a key)"
echo "  2. ssh-copy-id xeon@<THIS_VM_IP>"
echo "  3. docker context create xeon-dev --docker \"host=ssh://xeon@<THIS_VM_IP>\""
echo "  4. docker context use xeon-dev"
echo "  5. cd XFramework && docker compose up --build -d"
echo ""
echo "NOTE: Log out and back in (or run 'newgrp docker') for group changes to take effect."
