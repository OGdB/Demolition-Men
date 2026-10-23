using System;
using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Clips
{
    /// <summary>
    /// One scripted shot for the Clip Lab (docs/store-media.md §3): the level, where the
    /// players start and what they carry, how the camera frames it, and a timeline of steps.
    /// JSON in <c>content-work/shots/</c>. Steps run in lanes: lane 0 is the director (wait,
    /// slow motion, stills, end), lanes 1 and 2 are the players. Each lane runs its own steps
    /// in order, side by side with the others; the shot ends when lane 0 runs out.
    /// Times are video seconds (frames / fps): slow motion stretches game time, not the clip.
    /// </summary>
    [Serializable]
    public sealed class ShotSpec
    {
        public static readonly string[] DirectorSteps = { "wait", "slowmo", "still", "end" };
        public static readonly string[] PlayerSteps = { "walk", "hit", "tnt", "jump", "back", "wait", "swap", "bot" };
        public static readonly string[] Events = { "", "collapse", "break", "blast", "over" };
        public static readonly string[] Targets = { "", "weakpoint", "building", "end" };
        public static readonly string[] CameraModes = { "frame", "pan", "follow", "run" };
        public static readonly string[] Weapons = { "", "axe", "wreckingball" };

        public string id;
        /// <summary>Campaign level id (town-01 ...).</summary>
        public string level;
        /// <summary>0: the level's own seed.</summary>
        public int seed;
        public int players = 1;
        /// <summary>Index in <c>LevelLayout.Buildings</c> the shot is about; -1: the spawn.</summary>
        public int building = -1;
        /// <summary>The players start this far (units) from the building's left edge.</summary>
        public float offset = -5f;
        /// <summary>Every shop upgrade at this level (clamped to its maximum).</summary>
        public int upgrades;
        /// <summary>Sticks of dynamite each player carries.</summary>
        public int dynamite;
        public string weapon = "";
        public int weaponHits = 40;
        /// <summary>Show the HUD (and the touch controls with <see cref="touch"/>).</summary>
        public bool hud;
        public bool touch;
        public ShotCamera camera = new ShotCamera();
        public ShotStep[] steps = new ShotStep[0];
        /// <summary>Recorded frame. A square master is cropped to 9:16, 16:9 and 1:1 afterwards.</summary>
        public int width = 1920;
        public int height = 1920;
        public int fps = 60;
        /// <summary>Hard stop, video seconds.</summary>
        public float length = 20f;
        public int takes = 3;
        /// <summary>Stills are this many times the frame size (ScreenCapture's super size).</summary>
        public int stillScale = 1;

        public static ShotSpec FromJson(string json)
        {
            ShotSpec shot = JsonUtility.FromJson<ShotSpec>(json);
            // Fields a file leaves out can come back null inside arrays; the timeline compares with "".
            shot.weapon ??= "";
            shot.camera ??= new ShotCamera();
            shot.steps ??= new ShotStep[0];
            foreach (ShotStep step in shot.steps)
            {
                if (step == null)
                    continue;
                step.to ??= "";
                step.until ??= "";
                step.name ??= "";
            }
            return shot;
        }

        public IEnumerable<ShotStep> Lane(int lane)
        {
            foreach (ShotStep step in steps)
                if (step != null && step.p == lane)
                    yield return step;
        }

        /// <summary>Everything wrong with the shot, empty when it can be played.</summary>
        public List<string> Errors()
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(id)) errors.Add("no id");
            if (string.IsNullOrEmpty(level)) errors.Add("no level");
            if (players < 1 || players > 2) errors.Add($"players {players}: 1 or 2");
            if (width <= 0 || height <= 0 || width % 2 != 0 || height % 2 != 0) errors.Add($"frame {width}x{height}: positive and even (H.264)");
            if (fps < 24 || fps > 120) errors.Add($"fps {fps}: 24 to 120");
            if (length <= 0f) errors.Add("length must be positive");
            if (takes < 1) errors.Add("takes must be at least 1");
            if (stillScale < 1 || stillScale > 4) errors.Add($"stillScale {stillScale}: 1 to 4");
            if (Array.IndexOf(Weapons, weapon ?? "") < 0) errors.Add($"weapon '{weapon}': {string.Join(", ", Weapons)}");
            if (camera == null || Array.IndexOf(CameraModes, camera.mode) < 0) errors.Add($"camera mode '{camera?.mode}': {string.Join(", ", CameraModes)}");
            bool director = false;
            for (int i = 0; i < steps.Length; i++)
            {
                ShotStep step = steps[i];
                string at = $"step {i + 1} ({step?.@do})";
                if (step == null) { errors.Add($"step {i + 1} is empty"); continue; }
                if (step.p < 0 || step.p > players) { errors.Add($"{at}: lane {step.p} (0 = director, 1..{players} = players)"); continue; }
                director |= step.p == 0;
                if (Array.IndexOf(step.p == 0 ? DirectorSteps : PlayerSteps, step.@do) < 0)
                    errors.Add($"{at}: not a {(step.p == 0 ? "director" : "player")} step");
                if (Array.IndexOf(Events, step.until ?? "") < 0) errors.Add($"{at}: until '{step.until}'");
                if (Array.IndexOf(Targets, step.to ?? "") < 0) errors.Add($"{at}: to '{step.to}'");
                if (step.@do == "slowmo" && (step.scale <= 0f || step.scale > 1f)) errors.Add($"{at}: scale {step.scale}: above 0, at most 1");
                if (step.@do == "still" && string.IsNullOrEmpty(step.name)) errors.Add($"{at}: a still needs a name");
                if ((step.to == "weakpoint" || step.to == "building" || step.to == "end") && building < 0) errors.Add($"{at}: to '{step.to}' needs a building");
            }
            if (!director) errors.Add("no director step (lane 0): the shot would never end");
            return errors;
        }
    }

    [Serializable]
    public sealed class ShotCamera
    {
        /// <summary>frame: the building held still; pan: from <see cref="from"/> to <see cref="to"/>;
        /// follow: the front player; run: the run's own scrolling camera, out-of-bounds included.</summary>
        public string mode = "frame";
        /// <summary>Room (units) around the building in frame mode.</summary>
        public float pad = 2f;
        /// <summary>Orthographic size for pan and follow; 0 picks one.</summary>
        public float size;
        /// <summary>Pan start and end x, relative to the building's left edge (or the spawn).</summary>
        public float from;
        public float to;
        public float seconds = 6f;
        /// <summary>Follow: how far ahead of the front player the camera centres.</summary>
        public float lead = 2f;
    }

    [Serializable]
    public sealed class ShotStep
    {
        /// <summary>Lane: 0 the director, 1 and 2 the players.</summary>
        public int p = 1;
        public string @do;
        /// <summary>walk: weakpoint, building (its left edge), end (past its right edge); else <see cref="x"/>.</summary>
        public string to = "";
        /// <summary>walk: x relative to the building's left edge (or the spawn); jump: + moves right, - left.</summary>
        public float x;
        /// <summary>How long (wait, hit, back, jump, slowmo, bot), or the timeout of an <see cref="until"/>.</summary>
        public float seconds;
        /// <summary>wait / hit until: collapse (of at least <see cref="cells"/>), break, blast, over (the run ended).</summary>
        public string until = "";
        public int cells = 10;
        /// <summary>slowmo: the time scale.</summary>
        public float scale = 0.3f;
        /// <summary>still: the file name suffix.</summary>
        public string name = "";
    }

    /// <summary>What happened in a take, for <c>tools/media/media.py cut</c>: written next to the video.</summary>
    [Serializable]
    public sealed class ShotLog
    {
        public string shot;
        public int take;
        public int fps;
        public float length;
        public List<ShotEvent> events = new List<ShotEvent>();
        /// <summary>Steps that ran out of time instead of seeing their event or arriving: tune the shot.</summary>
        public List<string> timeouts = new List<string>();
    }

    [Serializable]
    public struct ShotEvent
    {
        /// <summary>Video seconds from the first recorded frame.</summary>
        public float t;
        /// <summary>collapse, break or blast.</summary>
        public string kind;
        public int cells;
        public float x;
        public float y;
    }
}
