using System;
using System.Collections;
using System.Collections.Generic;
using Demolition.Meta;
using UnityEngine;

namespace Demolition.Generation
{
    /// <summary>
    /// A furnished building's electrics. Where the theme lights its buildings, every sprite that
    /// gives light (a window of a lit room, a screen, a lamp, the roof sign) carries a glow: its
    /// lit overlay, drawn unlit so the dark doesn't dim it. A few of them also cast light on the
    /// room: <see cref="Sockets"/>, which the Demolition.Lighting assembly fills with 2D lights
    /// (<see cref="Spawned"/>). When the building comes down the power fails
    /// (<see cref="PowerCut"/>): the glows go out one after another from the break outward, and
    /// whatever runs on the power (<see cref="BuildingMusic"/>) stops with <see cref="Cut"/>.
    /// Cosmetic, and the same on both devices of an online run: each counts its own removed cells.
    /// </summary>
    public sealed class BuildingLights : MonoBehaviour
    {
        /// <summary>A glow that lights the room around it.</summary>
        public readonly struct Socket
        {
            /// <summary>The glow: a light under it goes out with it.</summary>
            public readonly Transform At;
            public readonly Color Color;
            public readonly float Radius;
            /// <summary>Changes colour on the beat (a disco ball, the spots).</summary>
            public readonly bool Disco;
            internal readonly int Priority, Storey;

            internal Socket(Transform at, Color color, float radius, bool disco, int priority, int storey)
            {
                At = at;
                Color = color;
                Radius = radius;
                Disco = disco;
                Priority = priority;
                Storey = storey;
            }
        }

        /// <summary>A building with lights was built: its <see cref="Sockets"/> want 2D lights.</summary>
        public static event Action<BuildingLights> Spawned;
        /// <summary>Some building's power failed at this spot (the sound of it).</summary>
        public static event Action<Vector3> PowerWentOut;
        /// <summary>This building's power failed.</summary>
        public event Action Cut;

        /// <summary>Past white, so the bloom picks the glows up.</summary>
        private const float Brightness = 1.5f;
        private const float Forward = -0.004f;
        private const float FlickerOff = 0.04f, FlickerOn = 0.07f;
        private static readonly Color Spark = new Color(1f, 0.95f, 0.6f);

        private struct Lamp
        {
            public GameObject Glow;
            public Vector2 At;
        }

        private readonly List<Lamp> _lamps = new List<Lamp>();
        private readonly List<Socket> _sockets = new List<Socket>();
        private DestructibleBuilding _building;
        private int _seed, _gone;

        public IReadOnlyList<Socket> Sockets => _sockets;
        public bool Powered { get; private set; } = true;

        /// <summary>2D lights per building: one per storey first, the sign before the lamps.</summary>
        private static int MaxSockets => Quality.Current == QualityTier.Low ? 2 : 4;

        public static BuildingLights On(DestructibleBuilding building, int seed)
        {
            var lights = building.gameObject.AddComponent<BuildingLights>();
            lights._building = building;
            lights._seed = seed;
            building.CellsRemoved += lights.OnCellsRemoved;
            return lights;
        }

        /// <summary>The glow of the sprite on <paramref name="parent"/>: its lit overlay, one
        /// sorting order up. <paramref name="at"/> is where it is in the building, in cells.</summary>
        public Transform Add(Transform parent, Sprite lit, int sortingOrder, Vector2 at)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, Forward);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = lit;
            sr.sharedMaterial = UnlitSprite.Material;
            sr.color = new Color(Brightness, Brightness, Brightness, 1f);
            sr.sortingOrder = sortingOrder;
            _lamps.Add(new Lamp { Glow = go, At = at });
            return go.transform;
        }

        /// <summary>A prop's glow, and its light on the room if it is a lamp or a sign.</summary>
        public void Add(Transform parent, SpecProp prop, BuildingTileset.Prop look, int sortingOrder)
        {
            Transform glow = Add(parent, look.lit, sortingOrder, prop.Rect.center);
            if (Casts(prop.Name, out float radius, out int priority, out bool disco))
                _sockets.Add(new Socket(glow, look.light, radius, disco, priority, prop.Storey));
        }

        // Which props light the room, how far, and which come first when lights are short.
        private static bool Casts(string name, out float radius, out int priority, out bool disco)
        {
            disco = name == "discoball" || name == "spot";
            priority = 2;
            switch (name)
            {
                case "discoball": radius = 5f; priority = 1; return true;
                case "spot": radius = 3.5f; priority = 3; return true;
                case "lamp": radius = 3.5f; return true;
                case "tube": radius = 4f; return true;
                case "neon": radius = 2.5f; priority = 3; return true;
            }
            radius = 4.5f;
            priority = 0;
            return name.StartsWith(Furnishing.SignPrefix);
        }

        /// <summary>Everything is added: keep the sockets the budget allows and announce them.</summary>
        public void Ready()
        {
            _sockets.Sort((a, b) => a.Priority != b.Priority ? a.Priority.CompareTo(b.Priority) : a.Storey.CompareTo(b.Storey));
            var kept = new List<Socket>();
            var storeys = new HashSet<int>();
            for (int pass = 0; pass < 2; pass++)
                foreach (Socket socket in _sockets)
                    if (kept.Count < MaxSockets && !kept.Contains(socket) && (pass == 1 || storeys.Add(socket.Storey)))
                        kept.Add(socket);
            _sockets.Clear();
            _sockets.AddRange(kept);
            if (_sockets.Count > 0)
                Spawned?.Invoke(this);
        }

        private void OnCellsRemoved(List<Vector2Int> cells)
        {
            if (!Powered || cells.Count == 0)
                return;
            _gone += cells.Count;
            if (!PowerCut.Trips(_building.InitialCellCount, _gone, cells.Count))
                return;
            Powered = false;
            var from = new Vector2(cells[0].x + 0.5f, cells[0].y + 0.5f); // the block that broke
            Vector3 world = transform.TransformPoint(from);
            for (int i = 0; i < _lamps.Count; i++)
                if (_lamps[i].Glow != null)
                {
                    uint hash = PowerCut.Hash(_seed, i);
                    StartCoroutine(Die(_lamps[i].Glow, PowerCut.Delay(_lamps[i].At, from, hash), PowerCut.Flickers(hash)));
                }
            Particles.Burst(world, Spark, 10, 6f, 0.08f, 0.35f);
            Cut?.Invoke();
            PowerWentOut?.Invoke(world);
        }

        private static IEnumerator Die(GameObject glow, float delay, int flickers)
        {
            yield return new WaitForSeconds(delay);
            for (int i = 0; i < flickers && glow != null; i++)
            {
                glow.SetActive(false);
                yield return new WaitForSeconds(FlickerOff);
                if (glow == null)
                    yield break;
                glow.SetActive(true);
                yield return new WaitForSeconds(FlickerOn);
            }
            if (glow != null)
                glow.SetActive(false);
        }
    }
}
