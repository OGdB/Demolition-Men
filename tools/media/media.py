"""Crops, highlights, contact sheets, GIFs, store screenshots and edits from the Clip Lab's
takes (docs/store-media.md §3). Python 3 with Pillow; ffmpeg and ffprobe on the PATH.

    python tools/media/media.py crop    content-work/media/raw/one-hit/take2.mp4 [--to 9x16,16x9,1x1]
    python tools/media/media.py cut     content-work/media/raw/broll-town-04/take1.mp4 [--top 5] [--before 2] [--after 3]
    python tools/media/media.py contact content-work/media/raw [--frames 6] [--out sheet.png]
    python tools/media/media.py gif     content-work/media/raw/one-hit/take2-16x9.mp4 --start 6 --length 6 [--width 800]
    python tools/media/media.py store   content-work/edits/store.json
    python tools/media/media.py edit    content-work/edits/short-one-hit.json

A take is <shot>/take<n>.mp4 with its event log <shot>/take<n>.json (the ClipDirector's ShotLog:
every collapse, break and blast at its video time). Paths in the JSON files are relative to the
repository root. Text on screen is drawn like the game's title (Black Ops One, gold, ink outline).

A cut list (edit) has "out", "size", "fps", optional "music", "musicStart", "musicVolume",
"gameVolume", "store" (refuse words Play bans) and "segments", each one of:
  {"file": take, "start": s, "end": e}            a stretch of a take, centre-cropped to the size
  {"file": take, "at": "collapse", "before": 2, "after": 3}   around its biggest collapse (or first blast/break)
  {"pair": [{"file": a, "start": s}, {"file": b, "start": s}], "seconds": d}   two devices at once
  {"image": png, "seconds": d}                    a card (the logo at the end)
and any of them may carry "text". Store screenshots (store) take "out", "ads" and "screenshots":
[{"image", "name", "caption", "alt"}].
"""
import argparse
import json
import os
import re
import subprocess
import sys
import tempfile

from PIL import Image, ImageDraw, ImageFont

sys.dont_write_bytecode = True  # no __pycache__ next to the tools
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "tools", "artpipe"))
from brand import FONT, INK, hazard_tape, wordmark  # noqa: E402

ASPECTS = {"9x16": (1080, 1920), "16x9": (1920, 1080), "1x1": (1080, 1080)}
ADS = {"1x1": (1200, 1200), "191x100": (1200, 628), "4x5": (1200, 1500)}
VIDEO = ["-c:v", "libx264", "-crf", "17", "-preset", "slow", "-pix_fmt", "yuv420p", "-movflags", "+faststart"]
AUDIO = ["-c:a", "aac", "-b:a", "192k", "-ar", "48000"]
BACKGROUND = INK[:3]
# Play refuses these in listing graphics and the preview video (ranking, price, calls to action).
STORE_BANNED = ["best", "#1", "top", "new", "free", "sale", "discount", "download", "install", "play now",
                "try now", "million", "award", "number one"]


def die(message):
    sys.exit(f"media: {message}")


def path(p):
    """A path from a JSON file: relative to the repository root."""
    return p if os.path.isabs(p) else os.path.join(ROOT, p)


def arg(p):
    """A path from the command line: relative to the current folder."""
    return os.path.abspath(p)


def ffmpeg(args):
    subprocess.run(["ffmpeg", "-y", "-hide_banner", "-loglevel", "error"] + args, check=True)


def probe(video):
    out = subprocess.run(["ffprobe", "-v", "error", "-show_streams", "-show_format", "-of", "json", video],
                         check=True, capture_output=True, text=True).stdout
    info = json.loads(out)
    v = next(s for s in info["streams"] if s["codec_type"] == "video")
    return {"width": int(v["width"]), "height": int(v["height"]), "duration": float(info["format"]["duration"]),
            "audio": any(s["codec_type"] == "audio" for s in info["streams"])}


def log_of(video):
    log = os.path.splitext(video)[0] + ".json"
    return json.load(open(log)) if os.path.exists(log) else None


def even(x):
    return max(2, int(round(x / 2.0)) * 2)


def crop_filter(src_w, src_h, out_w, out_h):
    """Centre crop to the output's shape, then scale to it."""
    aspect = out_w / out_h
    if src_w / src_h > aspect:
        cw, ch = even(src_h * aspect), src_h
    else:
        cw, ch = src_w, even(src_w / aspect)
    return f"crop={cw}:{ch}:(iw-{cw})/2:(ih-{ch})/2,scale={out_w}:{out_h}:flags=lanczos,setsar=1"


def claims(text):
    low = text.lower()
    return [w for w in STORE_BANNED if re.search(r"(?<![a-z0-9])" + re.escape(w) + r"(?![a-z0-9])", low)]


# ---- text drawn like the title ---------------------------------------------------------

def caption(text, max_w, max_h):
    """The title look (brand.wordmark) at the biggest size that fits, wrapped onto up to 3 lines."""
    words = text.replace("\n", " \n ").split(" ")
    for size in range(int(max_h), 11, -2):
        lines = wrap(words, size, max_w)
        if lines is None or len(lines) > 3:
            continue
        im = wordmark(lines, size, tape=False)
        if im.width <= max_w and im.height <= max_h:
            return im
    die(f"caption too long to fit: {text!r}")


def wrap(words, size, max_w):
    font = ImageFont.truetype(FONT, size)
    stroke = max(2, size // 10)
    lines, line = [], ""
    for word in words:
        if word == "\n":
            lines.append(line)
            line = ""
            continue
        if not word:
            continue
        test = f"{line} {word}".strip()
        box = font.getbbox(test, stroke_width=stroke)
        if box[2] - box[0] + 2 * stroke > max_w and line:
            lines.append(line)
            line = word
        else:
            line = test
    lines.append(line)
    return [l for l in lines if l]


def text_layer(text, w, h, y_share=None):
    """A full-frame transparent PNG with the text centred at y_share of the height: by default
    below the top bar Shorts and TikTok draw on 9:16, near the top on 16:9."""
    layer = Image.new("RGBA", (w, h))
    if y_share is None:
        y_share = 0.2 if h > w else 0.15
    if text:
        im = caption(text, int(w * 0.9), int(h * 0.16))
        layer.alpha_composite(im, ((w - im.width) // 2, max(0, int(h * y_share) - im.height // 2)))
    return layer


# ---- commands --------------------------------------------------------------------------

def cmd_crop(a):
    video = arg(a.video)
    info = probe(video)
    stem = os.path.splitext(video)[0]
    for name in a.to.split(","):
        if name not in ASPECTS:
            die(f"unknown format {name}: {', '.join(ASPECTS)}")
        out = f"{stem}-{name}.mp4"
        ffmpeg(["-i", video, "-vf", crop_filter(info["width"], info["height"], *ASPECTS[name])] + VIDEO
               + (AUDIO if info["audio"] else []) + [out])
        print(out)


def cmd_cut(a):
    video = arg(a.video)
    log = log_of(video) or die(f"no event log next to {video}")
    info = probe(video)
    events = [e for e in log["events"] if e["kind"] == a.kind and e["cells"] >= a.min_cells]
    events.sort(key=lambda e: (-e["cells"], e["t"]))
    picked = []
    for e in events:  # biggest first; a window overlapping a bigger one is skipped
        start, end = max(0.0, e["t"] - a.before), min(info["duration"], e["t"] + a.after)
        if all(end <= s or start >= t for s, t, _ in picked):
            picked.append((start, end, e))
        if len(picked) >= a.top:
            break
    if not picked:
        die(f"no {a.kind} of {a.min_cells}+ cells in {video}")
    stem = os.path.splitext(video)[0]
    for i, (start, end, e) in enumerate(sorted(picked, key=lambda p: p[0]), 1):
        out = f"{stem}-hl{i}.mp4"
        ffmpeg(["-ss", f"{start:.3f}", "-i", video, "-t", f"{end - start:.3f}"] + VIDEO
               + (AUDIO if info["audio"] else []) + [out])
        print(f"{out}: {e['kind']} of {e['cells']} cells at {e['t']:.1f}s")


def takes_in(target):
    if os.path.isfile(target):
        return [target]
    found = []
    for folder, _, files in os.walk(target):
        found += [os.path.join(folder, f) for f in files
                  if f.endswith(".mp4") and "-" not in os.path.splitext(f)[0]]  # raw takes only, not crops or cuts
    return sorted(found)


def cmd_contact(a):
    target = arg(a.target)
    videos = takes_in(target)
    base = target if os.path.isdir(target) else os.path.dirname(target)
    if not videos:
        die(f"no takes in {a.target}")
    thumb_h, label_w = 180, 420
    font = ImageFont.load_default()
    rows = []
    with tempfile.TemporaryDirectory() as tmp:
        for video in videos:
            info = probe(video)
            log = log_of(video)
            collapses = [e for e in (log or {}).get("events", []) if e["kind"] == "collapse"]
            if collapses:  # around the biggest collapse: before, the break, the fall, the dust
                t = max(collapses, key=lambda e: e["cells"])["t"]
                times = [t - 1.0, t - 0.2, t + 0.4, t + 1.0, t + 2.0, t + 3.5]
            else:
                times = [(i + 0.5) / 6 * info["duration"] for i in range(6)]
            times = [min(max(0.0, x), info["duration"] - 0.05) for x in times[:a.frames]]
            frames = []
            for i, t in enumerate(times):
                png = os.path.join(tmp, f"{len(rows)}-{i}.png")
                ffmpeg(["-ss", f"{t:.3f}", "-i", video, "-frames:v", "1", "-vf", f"scale=-2:{thumb_h}", png])
                frames.append((t, Image.open(png).convert("RGB")))
            lines = [os.path.relpath(video, base), f"{info['width']}x{info['height']}, {info['duration']:.1f}s"]
            if log:
                kinds = {}
                for e in log["events"]:
                    kinds[e["kind"]] = kinds.get(e["kind"], 0) + 1
                lines.append(", ".join(f"{n} {k}" for k, n in sorted(kinds.items())) or "no events")
                if collapses:
                    big = max(collapses, key=lambda e: e["cells"])
                    lines.append(f"biggest collapse {big['cells']} cells at {big['t']:.1f}s")
                if log.get("timeouts"):
                    lines.append("TIMED OUT: " + "; ".join(log["timeouts"]))
            rows.append((lines, frames))
    width = label_w + max(sum(f.width + 6 for _, f in frames) for _, frames in rows)
    sheet = Image.new("RGB", (width, len(rows) * (thumb_h + 24) + 10), BACKGROUND)
    d = ImageDraw.Draw(sheet)
    for r, (lines, frames) in enumerate(rows):
        y = 10 + r * (thumb_h + 24)
        for k, line in enumerate(lines):
            d.text((10, y + k * 16), line, fill=(255, 90, 80) if line.startswith("TIMED") else (225, 225, 225), font=font)
        x = label_w
        for t, im in frames:
            sheet.paste(im, (x, y))
            d.text((x + 4, y + thumb_h + 4), f"{t:.1f}s", fill=(200, 200, 200), font=font)
            x += im.width + 6
    out = arg(a.out) if a.out else os.path.join(base, "contact.png")
    sheet.save(out)
    print(out)


def cmd_gif(a):
    video = arg(a.video)
    out = arg(a.out) if a.out else os.path.splitext(video)[0] + ".gif"
    ffmpeg(["-ss", f"{a.start:.3f}", "-t", f"{a.length:.3f}", "-i", video, "-vf",
            f"fps={a.fps},scale={a.width}:-2:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff[p];"
            "[b][p]paletteuse=dither=bayer:bayer_scale=4", out])
    print(out, f"{os.path.getsize(out) / 1e6:.1f} MB")


def cmd_store(a):
    spec = json.load(open(arg(a.spec)))
    out_dir = path(spec.get("out", "content-work/media/store"))
    os.makedirs(out_dir, exist_ok=True)
    problems = []
    for shot in spec["screenshots"]:
        hits = claims(shot.get("caption", ""))
        if hits:
            problems.append(f"{shot['name']}: '{shot['caption']}' has {hits} (Play refuses ranking, price and call-to-action words)")
        if len(shot.get("alt", "")) > 140:
            problems.append(f"{shot['name']}: alt text is {len(shot['alt'])} characters (140 at most)")
    if problems:
        die("\n  " + "\n  ".join(problems))
    alt = []
    for shot in spec["screenshots"]:
        src = Image.open(path(shot["image"])).convert("RGB")
        src = crop_image(src, 16 / 9)
        w, h = src.size
        # Caption on an ink nameplate across the top (14%: Play allows 20%), hazard tape under
        # it, and the whole game frame below, HUD included.
        band = int(h * 0.14)
        tape = max(4, int(h * 0.012))
        sheet = Image.new("RGB", (w, h), BACKGROUND)
        game_h = h - band - tape
        game = src.resize((int(w * game_h / h), game_h), Image.LANCZOS)
        sheet.paste(game, ((w - game.width) // 2, band + tape))
        sheet.paste(hazard_tape(w, tape).convert("RGB"), (0, band))
        text = caption(shot["caption"], int(w * 0.9), int(band * 0.7))
        sheet.paste(text, ((w - text.width) // 2, (band - text.height) // 2), text)
        out = os.path.join(out_dir, shot["name"] + ".png")
        sheet.save(out)
        print(out)
        if shot.get("alt"):
            alt.append(f"{shot['name']}: {shot['alt']}")
        if spec.get("ads"):
            for name, (aw, ah) in ADS.items():
                ad = crop_image(Image.open(path(shot["image"])).convert("RGB"), aw / ah).resize((aw, ah), Image.LANCZOS)
                ad.save(os.path.join(out_dir, f"{shot['name']}-ad-{name}.png"))
    if alt:
        open(os.path.join(out_dir, "alt.txt"), "w").write("\n".join(alt) + "\n")


def crop_image(im, aspect):
    w, h = im.size
    if w / h > aspect:
        cw = int(h * aspect)
        return im.crop(((w - cw) // 2, 0, (w - cw) // 2 + cw, h))
    ch = int(w / aspect)
    return im.crop((0, (h - ch) // 2, w, (h - ch) // 2 + ch))


def cmd_edit(a):
    spec = json.load(open(arg(a.spec)))
    w, h = spec.get("size", [1080, 1920])
    fps = spec.get("fps", 60)
    out = path(spec["out"])
    os.makedirs(os.path.dirname(out), exist_ok=True)
    texts = [s.get("text", "") for s in spec["segments"]]
    if spec.get("store"):
        hits = [(t, claims(t)) for t in texts if claims(t)]
        if hits:
            die(f"store version: {hits} (no ranking, price or call-to-action words in the preview video)")
    inputs, filters, pairs = [], [], []
    total = 0.0
    with tempfile.TemporaryDirectory() as tmp:
        def add(args):
            inputs.extend(args)
            return sum(1 for x in inputs if x == "-i") - 1

        for i, seg in enumerate(spec["segments"]):
            if "image" in seg:
                dur = float(seg.get("seconds", 2.5))
                n = add(["-loop", "1", "-t", f"{dur:.3f}", "-framerate", str(fps), "-i", path(seg["image"])])
                filters.append(f"[{n}:v]scale={int(w * 0.8)}:{int(h * 0.6)}:force_original_aspect_ratio=decrease,"
                               f"pad={w}:{h}:(ow-iw)/2:(oh-ih)/2:color=0x{bytes(BACKGROUND).hex()},fps={fps},setsar=1,format=yuv420p[v{i}]")
                filters.append(f"anullsrc=r=48000:cl=stereo,atrim=duration={dur:.3f}[a{i}]")
                y_share = 0.8
            elif "pair" in seg:
                # Two devices at once (the phone host and the PC client): side by side on 16:9,
                # one above the other on 9:16. The sound is the first one's.
                dur = float(seg["seconds"])
                tall = h > w
                cw, ch = (w, even(h / 2)) if tall else (even(w / 2), h)
                legs = []
                for k, leg in enumerate(seg["pair"]):
                    leg_path = path(leg["file"])
                    n = add(["-ss", f"{leg.get('start', 0):.3f}", "-t", f"{dur:.3f}", "-i", leg_path])
                    filters.append(f"[{n}:v]scale={cw}:{ch}:force_original_aspect_ratio=decrease,"
                                   f"pad={cw}:{ch}:(ow-iw)/2:(oh-ih)/2:color=0x{bytes(BACKGROUND).hex()},fps={fps},setsar=1[p{i}_{k}]")
                    legs.append((n, probe(leg_path)["audio"]))
                filters.append(f"[p{i}_0][p{i}_1]{'vstack' if tall else 'hstack'}=inputs=2,format=yuv420p[v{i}]")
                n, has_audio = legs[0]
                filters.append(audio_of(n, has_audio, dur, i))
                y_share = None
            else:
                video = path(seg["file"])
                info = probe(video)
                start, end = moment(seg, video, info["duration"])
                dur = end - start
                n = add(["-ss", f"{start:.3f}", "-t", f"{dur:.3f}", "-i", video])
                filters.append(f"[{n}:v]{crop_filter(info['width'], info['height'], w, h)},fps={fps},format=yuv420p[v{i}]")
                filters.append(audio_of(n, info["audio"], dur, i))
                y_share = None
            layer = os.path.join(tmp, f"text{i}.png")
            text_layer(seg.get("text", ""), w, h, y_share).save(layer)
            n = add(["-loop", "1", "-t", f"{dur:.3f}", "-framerate", str(fps), "-i", layer])
            filters.append(f"[v{i}][{n}:v]overlay=0:0:shortest=1,format=yuv420p[t{i}]")
            pairs.append(f"[t{i}][a{i}]")
            total += dur
        filters.append(f"{''.join(pairs)}concat=n={len(pairs)}:v=1:a=1[v][game]")
        if spec.get("music"):
            n = add(["-ss", f"{spec.get('musicStart', 0):.3f}", "-i", path(spec["music"])])
            fade = max(0.0, total - 1.0)
            filters.append(f"[{n}:a]aresample=48000,aformat=channel_layouts=stereo,atrim=duration={total:.3f},"
                           f"afade=t=out:st={fade:.3f}:d=1,volume={spec.get('musicVolume', 0.7)}[music]")
            filters.append(f"[game]volume={spec.get('gameVolume', 1.0)}[g]")
            filters.append("[g][music]amix=inputs=2:duration=first:normalize=0[a]")
        else:
            filters.append("[game]anull[a]")
        ffmpeg(inputs + ["-filter_complex", ";".join(filters), "-map", "[v]", "-map", "[a]", "-r", str(fps)]
               + VIDEO + AUDIO + [out])
    print(f"{out}: {total:.1f}s")


def moment(seg, video, duration):
    """A segment's start and end: given, or around its take's biggest collapse (first blast,
    first break) with "at", so a cut list still fits after the take is recorded again."""
    if "at" not in seg:
        return float(seg.get("start", 0)), float(seg.get("end", duration))
    log = log_of(video) or die(f"{video}: 'at' needs the take's event log")
    events = [e for e in log["events"] if e["kind"] == seg["at"]]
    if not events:
        die(f"{video}: no {seg['at']} in its log")
    t = max(events, key=lambda e: (e["cells"], -e["t"]))["t"]
    return max(0.0, t - float(seg.get("before", 2))), min(duration, t + float(seg.get("after", 3)))


def audio_of(n, has_audio, dur, i):
    if has_audio:
        return f"[{n}:a]aresample=48000,aformat=channel_layouts=stereo,apad,atrim=duration={dur:.3f}[a{i}]"
    return f"anullsrc=r=48000:cl=stereo,atrim=duration={dur:.3f}[a{i}]"


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="command", required=True)
    c = sub.add_parser("crop", help="9:16, 16:9 and 1:1 versions of a take")
    c.add_argument("video")
    c.add_argument("--to", default="9x16,16x9,1x1")
    c.set_defaults(run=cmd_crop)
    c = sub.add_parser("cut", help="short clips around the biggest collapses in a take's log")
    c.add_argument("video")
    c.add_argument("--top", type=int, default=3)
    c.add_argument("--before", type=float, default=2.0)
    c.add_argument("--after", type=float, default=3.0)
    c.add_argument("--kind", default="collapse", choices=["collapse", "blast", "break"])
    c.add_argument("--min-cells", type=int, default=10)
    c.set_defaults(run=cmd_cut)
    c = sub.add_parser("contact", help="a sheet of frames from every take in a folder")
    c.add_argument("target")
    c.add_argument("--frames", type=int, default=6)
    c.add_argument("--out")
    c.set_defaults(run=cmd_contact)
    c = sub.add_parser("gif", help="a GIF for Reddit and the press kit")
    c.add_argument("video")
    c.add_argument("--start", type=float, default=0.0)
    c.add_argument("--length", type=float, default=8.0)
    c.add_argument("--width", type=int, default=800)
    c.add_argument("--fps", type=int, default=20)
    c.add_argument("--out")
    c.set_defaults(run=cmd_gif)
    c = sub.add_parser("store", help="captioned store screenshots (and ads images) from stills")
    c.add_argument("spec")
    c.set_defaults(run=cmd_store)
    c = sub.add_parser("edit", help="a Short or the trailer from a cut list")
    c.add_argument("spec")
    c.set_defaults(run=cmd_edit)
    a = p.parse_args()
    a.run(a)


if __name__ == "__main__":
    main()
