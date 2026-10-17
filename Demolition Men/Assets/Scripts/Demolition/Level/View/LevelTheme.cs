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
    /// <summary>One parallax layer of a theme's backdrop: a horizontally tileable picture.</summary>
    [System.Serializable]
    public sealed class BackdropLayer
    {
        public Sprite sprite;
        [Tooltip("0 = fixed in the world (moves past like the street), 1 = glued to the camera.")]
        [Range(0f, 1f)] public float parallax = 0.7f;
        [Tooltip("Bottom of the picture relative to the street, in units.")]
        public float bottom = -0.5f;
    }

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
        [Tooltip("Far to near. Empty = the shared backdrop prefab, tinted.")]
        public BackdropLayer[] backdrop = new BackdropLayer[0];
        public AudioClip music;
    }
}
