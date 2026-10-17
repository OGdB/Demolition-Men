#!/usr/bin/env bash
# Swaps the relay back to the previous image and restarts it. Running it again swaps forward.
#   rollback.sh <ssh-target>
set -euo pipefail
[ $# -eq 1 ] || { echo "usage: $0 <ssh-target>" >&2; exit 2; }
ssh -t "$1" 'sudo sh -c "set -e
docker image inspect demolition-relay:previous >/dev/null 2>&1 || { echo no previous image to roll back to >&2; exit 1; }
docker image tag demolition-relay:latest demolition-relay:swap
docker image tag demolition-relay:previous demolition-relay:latest
docker image tag demolition-relay:swap demolition-relay:previous
docker image rm demolition-relay:swap >/dev/null
docker compose --project-directory /opt/demolition-relay up -d --no-build
echo rolled back"'
