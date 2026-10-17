using Demolition.Generation;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Demolition.Levels
{
    /// <summary>
    /// One theme of the campaign: how its ground and buildings look, which building
    /// archetypes and levels it has, its sky and music. Content lives in
    /// <c>Assets/Content/&lt;Theme&gt;/</c>; the JSON stays the source of truth for rules.
    /// </summary>
    [CreateAssetMenu(menuName = "Demolition/Level Theme")]
    public sealed class LevelTheme : ScriptableObject
    {
        public string displayName = "TOWN";
        [Header("Ground")]
        public TileBase groundTop;
        public TileBase groundFill;
        [Header("Buildings")]
        public BuildingTileset tileset;
        public TextAsset[] archetypes = new TextAsset[0];
        [Header("Campaign")]
        [Tooltip("Level specs in play order.")]
        public TextAsset[] levels = new TextAsset[0];
        [Header("Mood")]
        public Color sky = new Color(0.55f, 0.75f, 0.9f);
        [Tooltip("Multiplied into the parallax backdrop.")]
        public Color backdropTint = Color.white;
        public AudioClip music;
    }
}
