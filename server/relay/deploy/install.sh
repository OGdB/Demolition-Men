#!/usr/bin/env bash
# Runs on the VPS as root; deploy.sh uploads it with the release and calls it:
#   install.sh <staging-dir> <domain> <firewall 0|1> <ssh-port> [email]
# Safe to re-run: every step checks first or overwrites.
set -euo pipefail
stage=$1 domain=$2 firewall=$3 ssh_port=$4 email=${5:-}
app=/opt/demolition-relay
trap 'rm -rf "$stage"' EXIT
export DEBIAN_FRONTEND=noninteractive

die() { echo "error: $*" >&2; exit 1; }

# Checks first: nothing on the server changes until they pass.
command -v ss >/dev/null || die "ss (iproute2) is missing; it is needed to see what holds ports 80/443"
held=$(ss -Hltnp '( sport = :80 or sport = :443 )' | grep -o 'users:(("[^"]*"' | cut -d'"' -f2 | grep -vx caddy | sort -u | tr '\n' ' ' || true)
if [ -n "$held" ]; then
  cat >&2 <<EOF
error: ports 80/443 are held by: $held
Caddy needs them, so nothing was changed. To keep that server, proxy the relay through it.
nginx, inside the server block for $domain (README: "Behind nginx"):

    location = /ws {
        proxy_pass http://127.0.0.1:5080;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header X-Forwarded-For \$remote_addr;
        proxy_read_timeout 1h;
    }
    location = /health { proxy_pass http://127.0.0.1:5080; }
EOF
  exit 1
fi
if ! command -v caddy >/dev/null; then
  # shellcheck source=/dev/null
  . /etc/os-release
  case " ${ID:-} ${ID_LIKE:-} " in
    *" debian "* | *" ubuntu "*) ;;
    *) die "Caddy is missing and this script installs it on Debian/Ubuntu only (found ${PRETTY_NAME:-unknown}). Install Caddy (https://caddyserver.com/docs/install), then re-run." ;;
  esac
fi

echo "--> Installing the relay to $app"
id relay >/dev/null 2>&1 || useradd --system --user-group --no-create-home --home-dir /nonexistent --shell /usr/sbin/nologin relay
# Owned by root, readable by relay. current/ becomes previous/ for rollback.sh.
install -d -m 755 "$app"
rm -rf "$app/next"
install -d -m 755 "$app/next"
install -m 755 "$stage/Relay" "$app/next/Relay"
install -m 644 "$stage/appsettings.json" "$app/next/appsettings.json"
rm -rf "$app/previous"
if [ -d "$app/current" ]; then mv "$app/current" "$app/previous"; fi
mv "$app/next" "$app/current"
install -m 644 "$stage/demolition-relay.service" /etc/systemd/system/demolition-relay.service

if ! command -v caddy >/dev/null; then
  echo "--> Installing Caddy from its apt repository"
  apt-get update -q
  apt-get install -y -q curl gnupg ca-certificates
  curl -fsSL https://dl.cloudsmith.io/public/caddy/stable/gpg.key | gpg --dearmor --yes -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
  curl -fsSL https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt > /etc/apt/sources.list.d/caddy-stable.list
  chmod o+r /usr/share/keyrings/caddy-stable-archive-keyring.gpg /etc/apt/sources.list.d/caddy-stable.list
  apt-get update -q
  apt-get install -y -q caddy
fi

echo "--> Configuring Caddy for $domain"
install -d -m 755 /etc/caddy/conf.d
{
  echo "$domain {"
  if [ -n "$email" ]; then printf '\ttls %s\n' "$email"; fi
  printf '\treverse_proxy 127.0.0.1:5080\n'
  echo "}"
} > /etc/caddy/conf.d/demolition-relay.caddy
caddyfile=/etc/caddy/Caddyfile
import='import /etc/caddy/conf.d/*.caddy'
# The stock file only serves /usr/share/caddy on :80; anything else is the owner's and only gets the import.
content=$(sed -e 's/#.*//' -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' "$caddyfile" 2>/dev/null | grep -v '^$' | tr '\n' ' ' || true)
if [ -z "$content" ] || [ "$content" = ':80 { root * /usr/share/caddy file_server } ' ]; then
  if [ -f "$caddyfile" ] && [ ! -f "$caddyfile.orig" ]; then cp "$caddyfile" "$caddyfile.orig"; fi
  echo "$import" > "$caddyfile"
elif ! grep -qxF "$import" "$caddyfile"; then
  printf '\n%s\n' "$import" >> "$caddyfile"
fi
caddy validate --adapter caddyfile --config "$caddyfile" >/dev/null 2>&1 \
  || die "Caddy rejects $caddyfile. See: caddy validate --adapter caddyfile --config $caddyfile"

if [ "$firewall" = 1 ]; then
  if command -v ufw >/dev/null; then
    echo "--> warning: enabling ufw. Incoming traffic is allowed only on SSH port $ssh_port, 80 and 443."
    ufw allow "$ssh_port/tcp"
    ufw allow 80/tcp
    ufw allow 443/tcp
    ufw --force enable
  else
    echo "--> warning: --firewall given but ufw is not installed; the firewall is unchanged."
  fi
elif command -v ufw >/dev/null && ufw status | grep -q 'Status: active'; then
  echo "--> note: ufw is active; ports 80 and 443 must be allowed for the certificate and for players."
fi

echo "--> Starting"
systemctl daemon-reload
systemctl enable --quiet demolition-relay caddy
systemctl restart demolition-relay
systemctl reload-or-restart caddy
sleep 2
if ! systemctl is-active --quiet demolition-relay; then
  journalctl -u demolition-relay -n 30 --no-pager >&2 || true
  die "the relay did not start. deploy/rollback.sh goes back to the previous release."
fi
