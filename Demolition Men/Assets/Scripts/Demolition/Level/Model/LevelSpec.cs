using System;
using UnityEngine;

namespace Demolition.Levels
{
    /// <summary>How many of each pickup a level hands out.</summary>
    [Serializable]
    public sealed class PickupBudget
    {
        public int dynamite = 3;
        public int axe = 1;
        public int wreckingBall = 1;
        /// <summary>Only placed in co-op.</summary>
        public int resurrect = 1;
    }

    /// <summary>
    /// A level as data (roadmap §5.2), read from <c>Assets/Content/&lt;Theme&gt;/Levels/*.json</c>.
    /// The pacing lists segment types left to right; <see cref="LevelGrammar"/> turns
    /// spec + seed into the same <see cref="LevelLayout"/> on every machine.
    /// </summary>
    [Serializable]
    public sealed class LevelSpec
    {
        public string id = "level";
        public string name = "LEVEL";
        public string theme = "Town";
        public int seed;
        /// <summary>Seconds; finishing faster earns the speed bonus.</summary>
        public float parTime = 120f;
        /// <summary>Camera auto-scroll speed (units/s) once pushed.</summary>
        public float scrollSpeed = 1.5f;
        /// <summary>Segment tokens: start, street, cluster:n, tower, gap, hill, sewer,
        /// gate, breather, finish.</summary>
        public string[] pacing = { "start", "street", "cluster:2", "finish" };
        public PickupBudget pickups = new PickupBudget();
        /// <summary>Archetype ids allowed in this level; empty = all of the theme's.</summary>
        public string[] archetypes = new string[0];

        public static LevelSpec FromJson(string json) => JsonUtility.FromJson<LevelSpec>(json);

        /// <summary>The same level with another seed (daily challenge, endless reroll).</summary>
        public LevelSpec WithSeed(int newSeed, string newId)
        {
            LevelSpec copy = FromJson(JsonUtility.ToJson(this));
            copy.seed = newSeed;
            copy.id = newId;
            return copy;
        }
    }
}
