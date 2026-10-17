#!/usr/bin/env bash
# Swaps the relay back to the previous release and restarts it. Running it again swaps forward.
#   rollback.sh <ssh-target>
set -euo pipefail
[ $# -eq 1 ] || { echo "usage: $0 <ssh-target>" >&2; exit 2; }
ssh -t "$1" 'sudo sh -c "set -e; cd /opt/demolition-relay
[ -d previous ] || { echo no previous release to roll back to >&2; exit 1; }
rm -rf swap; mv current swap; mv previous current; mv swap previous
systemctl restart demolition-relay; echo rolled back"'
