#!/usr/bin/env bash
# Swaps the ranked server back to the previous image and restarts it. Running it again swaps forward.
# The database stays as it is.
#   rollback.sh <ssh-target>
set -euo pipefail
[ $# -eq 1 ] || { echo "usage: $0 <ssh-target>" >&2; exit 2; }
ssh -t "$1" 'sudo sh -c "set -e
docker image inspect demolition-ranked:previous >/dev/null 2>&1 || { echo no previous image to roll back to >&2; exit 1; }
docker image tag demolition-ranked:latest demolition-ranked:swap
docker image tag demolition-ranked:previous demolition-ranked:latest
docker image tag demolition-ranked:swap demolition-ranked:previous
docker image rm demolition-ranked:swap >/dev/null
docker compose --project-directory /opt/demolition-ranked up -d --no-build
echo rolled back"'
