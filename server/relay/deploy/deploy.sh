#!/usr/bin/env bash
# Deploys or updates the relay on a Debian/Ubuntu VPS behind Caddy (TLS by Let's Encrypt).
# Run from the dev machine; safe to re-run, and re-running is how you update.
#   deploy.sh <ssh-target> <domain> [--email you@example.org] [--firewall]
set -euo pipefail

die() { echo "error: $*" >&2; exit 1; }
usage() { die "usage: $0 <ssh-target> <domain> [--email you@example.org] [--firewall]"; }

[ $# -ge 2 ] || usage
target=$1 domain=$2
shift 2
email="" firewall=0
while [ $# -gt 0 ]; do
  case $1 in
    --email) [ $# -ge 2 ] || usage; email=$2; shift 2 ;;
    --firewall) firewall=1; shift ;;
    *) usage ;;
  esac
done
# Both end up in the Caddyfile and on a remote command line.
[[ $domain =~ ^[A-Za-z0-9]([A-Za-z0-9.-]*[A-Za-z0-9])?$ && $domain == *.* ]] || die "not a domain name: $domain"
[[ -z $email || $email =~ ^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+$ ]] || die "not an email address: $email"

here=$(cd "$(dirname "$0")" && pwd)
export PATH="$HOME/.dotnet:$PATH"
command -v dotnet >/dev/null || die "building the relay needs the .NET 10 SDK: https://dot.net"

echo "==> Connecting to $target"
# Last line only: login scripts may print a banner first.
info=$(ssh "$target" 'echo "$(uname -m) ${SSH_CONNECTION##* } $(mktemp -d)"' | tail -n 1)
read -r arch ssh_port stage <<<"$info"
[[ $ssh_port =~ ^[0-9]+$ && $stage == /* ]] || die "unexpected answer from $target: $info"
case $arch in
  x86_64) rid=linux-x64 ;;
  aarch64 | arm64) rid=linux-arm64 ;;
  *) die "unsupported CPU architecture on the VPS: $arch" ;;
esac

echo "==> Building for $rid (self-contained: the VPS needs no .NET)"
out=$(mktemp -d)
trap 'rm -rf "$out"' EXIT
dotnet publish "$here/../Relay/Relay.csproj" -c Release -r "$rid" --self-contained \
  -p:PublishSingleFile=true -p:DebugType=None -o "$out" -nologo -v q

echo "==> Uploading"
scp -C -q "$out/Relay" "$out/appsettings.json" "$here/demolition-relay.service" "$here/install.sh" "$target:$stage/"

echo "==> Installing (sudo may ask for your password)"
ssh -t "$target" sudo bash "$stage/install.sh" "$stage" "$domain" "$firewall" "$ssh_port" ${email:+"$email"}

echo "==> Waiting for https://$domain/health (the first certificate can take a minute)"
for _ in $(seq 30); do
  if [ "$(curl -fsS --max-time 5 "https://$domain/health" 2>/dev/null)" = ok ]; then
    echo "Relay is up. The game connects to: wss://$domain/ws"
    exit 0
  fi
  sleep 2
done
die "no answer from https://$domain/health. Check that $domain points at the VPS and that ports 80 and 443 are open (also in the provider's firewall), then: ssh $target journalctl -u caddy -n 50"
