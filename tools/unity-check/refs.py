#!/usr/bin/env python3
"""Check hand-edited Unity YAML (scenes, prefabs, assets) for broken references.

    tools/unity-check/refs.py --since <git rev>     # every scene/prefab/asset changed since rev
    tools/unity-check/refs.py path/a.prefab ...     # just these files

Flags GUIDs that no .meta in the project (or in the packages fetched by setup.sh) declares,
and local fileIDs that point at no object in the same file. Exit 1 on any problem.
GUIDs of packages setup.sh does not fetch (2D, TextMesh Pro resources...) are reported as
unresolved too; judge those by eye.
"""
import glob
import os
import re
import subprocess
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ASSETS = os.path.join(REPO, "Demolition Men", "Assets")
CHECK_HOME = os.environ.get("UNITY_CHECK_HOME", os.path.expanduser("~/.unity-check"))


def known_guids():
    guids = set()
    metas = glob.glob(ASSETS + "/**/*.meta", recursive=True) + glob.glob(CHECK_HOME + "/com.unity.*/**/*.meta", recursive=True)
    for meta in metas:
        with open(meta, errors="replace") as f:
            m = re.search(r"^guid: (\w+)", f.read(), re.M)
        if m:
            guids.add(m.group(1))
    return guids


def files_since(rev):
    out = subprocess.run(["git", "diff", "--name-only", rev, "--", "Demolition Men"], cwd=REPO, capture_output=True, text=True).stdout
    return [os.path.join(REPO, f) for f in out.split("\n") if f.endswith((".unity", ".prefab", ".asset"))]


def main(argv):
    files = files_since(argv[1]) if len(argv) >= 2 and argv[0] == "--since" else argv
    known = known_guids()
    problems = 0
    for path in files:
        if not os.path.exists(path):
            continue
        text = open(path, errors="replace").read()
        ids = set(re.findall(r"^--- !u!\d+ &(-?\d+)", text, re.M))
        for guid in sorted(set(re.findall(r"guid: (\w{32})", text))):
            if guid not in known and not guid.startswith("0000000000000000"):
                print(f"unresolved guid {guid}  {os.path.relpath(path, REPO)}")
                problems += 1
        for local in sorted(set(re.findall(r"\{fileID: (-?\d+)\}", text))):
            if local != "0" and local not in ids:
                print(f"dangling fileID {local}  {os.path.relpath(path, REPO)}")
                problems += 1
    print(f"{len(files)} file(s) checked, {problems} problem(s)")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
