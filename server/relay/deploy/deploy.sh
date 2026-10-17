#!/usr/bin/env bash
# Deploys or updates the relay as a Docker container behind the VPS's existing Caddy
# container. Run from the dev machine; safe to re-run, and re-running is how you update.
#   deploy.sh <ssh-target> <domain> [--network web] [--caddy caddy] [--caddyfile /opt/portainer/Caddyfile]
set -euo pipefail

die() { echo "error: $*" >&2; exit 1; }
usage() { die "usage: $0 <ssh-target> <domain> [--network web] [--caddy caddy] [--caddyfile /opt/portainer/Caddyfile]"; }

[ $# -ge 2 ] || usage
target=$1 domain=$2
shift 2
network=web caddy=caddy caddyfile=/opt/portainer/Caddyfile
while [ $# -gt 0 ]; do
  [ $# -ge 2 ] || usage
  case $1 in
    --network) network=$2 ;;
    --caddy) caddy=$2 ;;
    --caddyfile) caddyfile=$2 ;;
    *) usage ;;
  esac
  shift 2
done
# All of these end up in the Caddyfile or on a remote command line.
[[ $domain =~ ^[A-Za-z0-9]([A-Za-z0-9.-]*[A-Za-z0-9])?$ && $domain == *.* ]] || die "not a domain name: $domain"
[[ $network =~ ^[A-Za-z0-9_.-]+$ && $caddy =~ ^[A-Za-z0-9_.-]+$ ]] || die "bad network or container name"
[[ $caddyfile =~ ^/[A-Za-z0-9_./-]+$ ]] || die "bad Caddyfile path: $caddyfile"

src=$(cd "$(dirname "$0")/.." && pwd)

echo "==> Connecting to $target"
# The VPS resolves the name: Git Bash on Windows has no getent or dig. Last line only:
# login scripts may print a banner first.
# shellcheck disable=SC2029 # $domain (validated above) goes into the remote command on purpose
info=$(ssh "$target" "echo \$(mktemp -d) \$(getent ahostsv4 $domain | awk 'NR == 1 { print \$1 }' | grep . || echo none) \$(hostname -I)" | tail -n 1)
read -r stage ip addresses <<<"$info"
[[ $stage == /tmp/* ]] || die "unexpected answer from $target: $info"
[ "$ip" != none ] || die "$domain has no A record yet. Add one pointing at the VPS, wait for it to resolve, then re-run: Caddy can't get a certificate without it."
[[ " $addresses " == *" $ip "* ]] || echo "warning: $domain points at $ip, which isn't an address of this VPS ($addresses). Fine for a floating IP; otherwise fix the A record."

echo "==> Uploading the sources (the VPS builds the image)"
# shellcheck disable=SC2029 # $stage is the VPS's own path, expanded here on purpose
tar -C "$src" --exclude=bin --exclude=obj -czf - Dockerfile .dockerignore compose.yaml Relay deploy/install.sh \
  | ssh "$target" "tar -xzf - -C '$stage'"

echo "==> Installing (sudo may ask for your password)"
ssh -t "$target" sudo bash "$stage/deploy/install.sh" "$stage" "$domain" "$network" "$caddy" "$caddyfile"

echo "==> Waiting for https://$domain/health (the first certificate can take a minute)"
for _ in $(seq 30); do
  if [ "$(curl -fsS --max-time 5 "https://$domain/health" 2>/dev/null)" = ok ]; then
    echo "Relay is up. The game connects to: wss://$domain/ws"
    exit 0
  fi
  sleep 2
done
die "no answer from https://$domain/health. Check the A record, then the certificate: ssh $target docker logs --tail 50 $caddy"
