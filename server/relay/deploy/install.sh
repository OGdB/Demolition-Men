#!/usr/bin/env bash
# Runs on the VPS as root; deploy.sh uploads the sources and calls it:
#   install.sh <staging-dir> <domain> <network> <caddy-container> <caddyfile-on-host>
# Safe to re-run: it rebuilds the image and adds the Caddy site only once.
set -euo pipefail
stage=$1 domain=$2 network=$3 caddy=$4 caddyfile=$5
app=/opt/demolition-relay
trap 'rm -rf "$stage"' EXIT

die() { echo "error: $*" >&2; exit 1; }

# Checks first: nothing on the server changes until they pass.
command -v docker >/dev/null || die "docker is missing"
docker compose version >/dev/null 2>&1 || die "the docker compose plugin is missing"
[ "$(docker inspect -f '{{.State.Running}}' "$caddy" 2>/dev/null)" = true ] || die "the container '$caddy' isn't running"
docker inspect -f '{{range $name, $_ := .NetworkSettings.Networks}}{{$name}} {{end}}' "$caddy" | grep -qw -- "$network" \
  || die "'$caddy' isn't on the Docker network '$network' (--network)"
subnet=$(docker network inspect -f '{{range .IPAM.Config}}{{.Subnet}} {{end}}' "$network" | tr ' ' '\n' | grep -v ':' | head -n 1 || true)
[ -n "$subnet" ] || die "the network '$network' has no IPv4 subnet"
[ -f "$caddyfile" ] || die "$caddyfile not found (--caddyfile)"
inner=$(docker inspect -f '{{range .Mounts}}{{if eq .Source "'"$caddyfile"'"}}{{.Destination}}{{end}}{{end}}' "$caddy")
[ -n "$inner" ] || die "$caddyfile isn't mounted into '$caddy' (--caddyfile)"
# A single-file bind mount keeps the inode it started with: if the file was since replaced
# (most editors, sed -i), the container still reads the old copy and never sees our block.
[ "$(stat -c %i "$caddyfile")" = "$(docker exec "$caddy" stat -c %i "$inner")" ] \
  || die "'$caddy' still reads an older copy of $caddyfile (the file was replaced since it started). Run: docker restart $caddy (all its sites blink for a few seconds), then re-run."

echo "--> Building and starting the relay container"
install -d -m 755 "$app"
rm -rf "$app/Relay"
cp -r "$stage/Dockerfile" "$stage/.dockerignore" "$stage/compose.yaml" "$stage/Relay" "$app/"
printf 'RELAY_NETWORK=%s\nRELAY_TRUSTED_PROXY_NETWORK=%s\n' "$network" "$subnet" > "$app/.env"
touch "$app/relay.env"
# The image running now becomes :previous, for rollback.sh.
if docker image inspect demolition-relay:latest >/dev/null 2>&1; then
  docker image tag demolition-relay:latest demolition-relay:previous
fi
docker compose --project-directory "$app" up -d --build --remove-orphans
docker image prune -f --filter label=org.opencontainers.image.title=demolition-relay >/dev/null

echo "--> Checking that $caddy reaches the relay"
for i in $(seq 15); do
  if [ "$(docker exec "$caddy" wget -qO- -T 3 http://demolition-relay:5080/health 2>/dev/null)" = ok ]; then break; fi
  [ "$i" -lt 15 ] || { docker logs --tail 30 demolition-relay >&2 || true; die "the relay doesn't answer on demolition-relay:5080. ../rollback.sh goes back to the previous image."; }
  sleep 1
done

if grep -qE "^[[:space:]]*${domain//./\\.}[[:space:],{]|^[[:space:]]*${domain//./\\.}$" "$caddyfile"; then
  echo "--> $domain is already in $caddyfile; left as is"
else
  echo "--> Adding $domain to $caddyfile"
  backup="$caddyfile.bak.$(date +%Y%m%d%H%M%S)"
  cp -p "$caddyfile" "$backup"
  # Append in place: the file is bind-mounted into the container by inode, so a
  # replaced file (sed -i, most editors) would never show up inside it.
  cat >> "$caddyfile" <<CADDY

# Demolition Men co-op relay (server/relay in the game's repository)
$domain {
	reverse_proxy demolition-relay:5080 {
		# A config reload closes proxied WebSockets after this delay: games in progress keep running.
		stream_close_delay 30m
	}
}
CADDY
  if ! docker exec "$caddy" caddy validate --config "$inner" --adapter caddyfile >/dev/null 2>&1; then
    cat "$backup" > "$caddyfile" # in place, same inode
    die "Caddy rejected the new site, so $caddyfile is back as it was. See: docker exec $caddy caddy validate --config $inner --adapter caddyfile"
  fi
fi

echo "--> Reloading Caddy"
docker exec "$caddy" caddy reload --config "$inner" --adapter caddyfile \
  || die "caddy reload failed (is its admin API off?). The site is in $caddyfile; restart the container to load it: docker restart $caddy"
