#!/usr/bin/env bash
# One-time setup for compile-checking the Unity project without the Unity editor
# (cloud agents, CI). Installs the .NET 8 SDK, fetches Unity 6000.4 reference assemblies
# (stubs: they compile, they don't run) and builds the Input System and uGUI/TMP packages
# from source against them. Everything lands in $UNITY_CHECK_HOME (default ~/.unity-check).
set -euo pipefail
HOME_DIR="${UNITY_CHECK_HOME:-$HOME/.unity-check}"
UNITY_MODULES_VERSION="${UNITY_MODULES_VERSION:-6000.4.9}"
INPUTSYSTEM_VERSION="${INPUTSYSTEM_VERSION:-1.19.0}"
UGUI_VERSION="${UGUI_VERSION:-3.0.0-exp.4}"
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

fetch() { # package version
  [ -d "$1" ] && return
  url=$(curl -sS "https://download.packages.unity.com/$1" | python3 -c "import json,sys; print(json.load(sys.stdin)['versions']['$2']['dist']['tarball'])")
  mkdir -p "$1" && curl -sSL "$url" | tar xz -C "$1"
}
fetch com.unity.inputsystem "$INPUTSYSTEM_VERSION"
fetch com.unity.ugui "$UGUI_VERSION"

# The registry's uGUI is a newer cut than the editor-bundled one; three spots need patching
# for it to compile against 6000.4 (only the UI Toolkit bridge and a TMP name clash).
UGUI=com.unity.ugui/package/Runtime
sed -i 's/typeof(PanelEventHandler), typeof(PanelRaycaster)/typeof(Transform)/' "$UGUI/UGUI/EventSystem/EventSystem.cs"
for f in TMP_Text TMP_Settings FontFeatureCommon TextMeshProUGUI TextMeshPro; do
  grep -q "using OTL_FeatureTag" "$UGUI/TMP/$f.cs" || sed -i '0,/^using /s//using OTL_FeatureTag = UnityEngine.TextCore.OTL_FeatureTag;\nusing /' "$UGUI/TMP/$f.cs"
done
echo "unity-check ready in $HOME_DIR"
