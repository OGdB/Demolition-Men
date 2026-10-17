#!/usr/bin/env bash
# Compile-check the project's runtime assemblies (Demolition, Assembly-CSharp) against
# Unity's reference assemblies, then run the EditMode model tests on plain .NET.
#   tools/unity-check/check.sh            build + tests
#   tools/unity-check/check.sh build      build only
#   tools/unity-check/check.sh test -- --where "class == Demolition.Tests.MetaTests"
# Run setup.sh once first. Editor-only code (Assets/Editor) is not covered.
set -uo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
export UNITY_CHECK_HOME="${UNITY_CHECK_HOME:-$HOME/.unity-check}"
export PATH="$HOME/.dotnet:$PATH"
mode="${1:-all}"; shift || true
[ "${1:-}" = "--" ] && shift

build() { # project
  local out
  out=$(cd "$HERE/projects" && dotnet build "$1.csproj" -v q -nologo 2>&1)
  local errs
  errs=$(echo "$out" | grep -E " error " | sed 's#.*/Assets/#Assets/#; s# \[/.*##' | sort -u)
  if [ -n "$errs" ]; then echo "FAIL $1"; echo "$errs"; return 1; fi
  echo "ok   $1"
}

rc=0
if [ "$mode" = all ] || [ "$mode" = build ]; then
  for p in UnityEngine.UI Unity.TextMeshPro Unity.InputSystem; do
    [ -f "$UNITY_CHECK_HOME/out/$p/$p.dll" ] || build "$p" || exit 1
  done
  build Demolition || rc=1
  [ $rc -eq 0 ] && { build Assembly-CSharp || rc=1; }
fi
if [ "$mode" = all ] || [ "$mode" = test ]; then
  build ModelTests || exit 1
  out=$(cd "$HERE/../../Demolition Men" && dotnet "$UNITY_CHECK_HOME/out/ModelTests/ModelTests.dll" --noresult --labels=Off "$@" 2>&1)
  echo "$out" | sed -n '/Errors, Failures/,/^Run Settings/p;/^Test Run Summary/,/Failed Tests/p'
  echo "$out" | grep -q "Overall result: Passed" || rc=1
fi
exit $rc
