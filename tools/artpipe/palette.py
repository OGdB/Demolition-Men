"""Master palette shared by every theme.

Colours are grouped in ramps ordered dark -> light. Step 0 is the darkest colour of a
ramp and doubles as its outline/mortar colour. Light comes from the top-left: highlights
go on top/left edges, shadows on bottom/right.

A theme uses a subset of the ramps plus 2-3 accent ramps (see themes/*.json). QA checks
every sprite of a theme against exactly that subset, so themes stay one game.
"""

RAMPS = {
    # Anchor ramps lifted from the hand-drawn object set (Town).
    "brick":      [(109, 25, 25), (164, 14, 14), (205, 32, 32), (250, 108, 70)],
    "wood":       [(64, 36, 8), (109, 67, 25), (164, 111, 58), (217, 160, 102)],
    "steel":      [(51, 51, 51), (80, 87, 87), (112, 121, 123), (155, 173, 183)],
    # Day materials.
    "concrete":   [(66, 66, 76), (106, 106, 114), (146, 144, 148), (188, 186, 184)],
    "rust":       [(70, 30, 22), (124, 52, 30), (178, 86, 42), (224, 138, 74)],
    "asphalt":    [(16, 16, 24), (32, 32, 42), (50, 50, 62), (74, 74, 88)],
    # Accents.
    "hazard":     [(122, 46, 16), (206, 90, 24), (246, 142, 40), (255, 204, 86)],
    "sea":        [(14, 40, 50), (22, 82, 92), (38, 130, 140), (150, 222, 216)],
    "warm":       [(118, 58, 26), (210, 126, 48), (250, 188, 84), (255, 236, 164)],
    "magenta":    [(104, 18, 90), (212, 44, 166), (255, 132, 226)],
    "cyan":       [(14, 92, 124), (46, 212, 236), (198, 255, 255)],
    # Night materials.
    "night":      [(24, 18, 40), (44, 34, 70), (68, 56, 104), (102, 88, 144)],
    "nightglass": [(14, 18, 46), (28, 42, 90), (52, 80, 146)],
}

MASTER = [c for ramp in RAMPS.values() for c in ramp]
assert len(set(MASTER)) == len(MASTER), "duplicate colour in master palette"


def theme_ramp_names(theme):
    """Ramps a theme may use: its base subset plus its accents."""
    return list(theme["ramps"]) + list(theme["accents"])


def theme_colors(theme):
    """Set of RGB tuples a theme's sprites may contain."""
    return {c for name in theme_ramp_names(theme) for c in RAMPS[name]}


def ramp(theme, name):
    """A ramp the theme is allowed to use; raises if the theme did not declare it."""
    if name not in theme_ramp_names(theme):
        raise KeyError(f"theme {theme['name']} does not declare ramp '{name}'")
    return RAMPS[name]


if __name__ == "__main__":
    print(f"{len(MASTER)} colours in {len(RAMPS)} ramps")
    for name, r in RAMPS.items():
        print(f"  {name:11s} {r}")
