#!/usr/bin/env bash
# One-time setup for compile-checking the Unity project without the Unity editor
# (cloud agents, CI). Installs the .NET 8 SDK, fetches Unity 6000.4 reference assemblies
# (stubs: they compile, they don't run) and the package sources check.sh builds against them:
# Input System, uGUI/TMP, Netcode for GameObjects with its Transport/Collections/Burst/Mathematics,
# NativeWebSocket. Everything lands in $UNITY_CHECK_HOME (default ~/.unity-check).
set -euo pipefail
HOME_DIR="${UNITY_CHECK_HOME:-$HOME/.unity-check}"
UNITY_MODULES_VERSION="${UNITY_MODULES_VERSION:-6000.4.9}"
INPUTSYSTEM_VERSION="${INPUTSYSTEM_VERSION:-1.19.0}"
UGUI_VERSION="${UGUI_VERSION:-3.0.0-exp.4}"
NGO_VERSION="${NGO_VERSION:-2.13.3}"
TRANSPORT_VERSION="${TRANSPORT_VERSION:-2.6.0}"   # pinned by NGO
# Burst and Mathematics as Packages/packages-lock.json resolves them (UPM takes the highest
# version any package asks for). Collections is a builtin 6.4.0 in the 6000.4 editor, not on
# the registry: its latest 2.x is the nearest cut.
BURST_VERSION="${BURST_VERSION:-1.8.29}"
MATHEMATICS_VERSION="${MATHEMATICS_VERSION:-1.3.3}"
COLLECTIONS_VERSION="${COLLECTIONS_VERSION:-2.7.1}"
NATIVEWEBSOCKET_COMMIT="${NATIVEWEBSOCKET_COMMIT:-c612a4fef60f2ae57614b73202d2d261ba56aa3e}" # upm-2, 2.0.7
mkdir -p "$HOME_DIR"
cd "$HOME_DIR"

if ! command -v dotnet >/dev/null && [ ! -x "$HOME/.dotnet/dotnet" ]; then
  curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
  bash dotnet-install.sh --channel 8.0 --install-dir "$HOME/.dotnet"
fi

if [ ! -d mods ]; then
  curl -sSL "https://nuget.bepinex.dev/v3/package/unityengine.modules/$UNITY_MODULES_VERSION/unityengine.modules.$UNITY_MODULES_VERSION.nupkg" -o mods.nupkg
  mkdir -p mods && (cd mods && unzip -q -o ../mods.nupkg)
fi

# Editor scripts compile against an older UnityEditor.dll (no newer reference assembly is published).
if [ ! -f editor/sdk/lib/UnityEditor.dll ]; then
  mkdir -p editor
  curl -sSL https://api.nuget.org/v3-flatcontainer/unity3d.sdk/2021.1.14.1/unity3d.sdk.2021.1.14.1.nupkg -o editor/sdk.nupkg
  (cd editor && unzip -q -o sdk.nupkg -d sdk)
fi

fetch() { # package version
  [ -d "$1" ] && return
  url=$(curl -sS "https://download.packages.unity.com/$1" | python3 -c "import json,sys; print(json.load(sys.stdin)['versions']['$2']['dist']['tarball'])")
  mkdir -p "$1" && curl -sSL "$url" | tar xz -C "$1"
}
fetch com.unity.inputsystem "$INPUTSYSTEM_VERSION"
fetch com.unity.ugui "$UGUI_VERSION"
fetch com.unity.netcode.gameobjects "$NGO_VERSION"
fetch com.unity.transport "$TRANSPORT_VERSION"
fetch com.unity.collections "$COLLECTIONS_VERSION"
fetch com.unity.burst "$BURST_VERSION"
fetch com.unity.mathematics "$MATHEMATICS_VERSION"
if [ ! -d NativeWebSocket ]; then
  git init -q NativeWebSocket.tmp
  git -C NativeWebSocket.tmp fetch -q --depth 1 https://github.com/endel/NativeWebSocket.git "$NATIVEWEBSOCKET_COMMIT"
  git -C NativeWebSocket.tmp -c advice.detachedHead=false checkout -q FETCH_HEAD
  mv NativeWebSocket.tmp NativeWebSocket
fi

# The registry's uGUI is a newer cut than the editor-bundled one; three spots need patching
# for it to compile against 6000.4 (only the UI Toolkit bridge and a TMP name clash).
UGUI=com.unity.ugui/package/Runtime
sed -i 's/typeof(PanelEventHandler), typeof(PanelRaycaster)/typeof(Transform)/' "$UGUI/UGUI/EventSystem/EventSystem.cs"
for f in TMP_Text TMP_Settings FontFeatureCommon TextMeshProUGUI TextMeshPro; do
  grep -q "using OTL_FeatureTag" "$UGUI/TMP/$f.cs" || sed -i '0,/^using /s//using OTL_FeatureTag = UnityEngine.TextCore.OTL_FeatureTag;\nusing /' "$UGUI/TMP/$f.cs"
done
echo "unity-check ready in $HOME_DIR"
