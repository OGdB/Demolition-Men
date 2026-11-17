using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// The spray-painted X on a block: "hit here" for players who don't know yet that one hit
    /// can drop a building (docs/hit-plan.md F1). Paint only: a sprite under the block's visual,
    /// so it leans and trembles with the block and goes with it; no collider, no rule.
    /// <see cref="Levels.LevelBuilder"/> paints the first buildings of a level that asks for it
    /// (<c>LevelSpec.weakMarks</c>); on a block of a hand-made building (the Tutorial) this
    /// component paints its own block.
    /// </summary>
    public sealed class WeakPointMark : MonoBehaviour
    {
        /// <summary>Made by tools/artpipe/marks.py.</summary>
        private const string SpritePath = "Marks/WeakPoint";

        private static Sprite _sprite;

        private void Start()
        {
            var block = GetComponent<BuildingBlock>();
            if (block != null)
                Paint(block);
            else
                Debug.LogWarning($"{name}: a WeakPointMark belongs on a BuildingBlock.", this);
        }

        public static void Paint(BuildingBlock block)
        {
            Transform visual = block.Visual;
            SpriteRenderer on = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            if (on == null || on.sprite == null)
                return;
            if (_sprite == null)
                _sprite = Resources.Load<Sprite>(SpritePath);

            var go = new GameObject("WeakPointMark");
            Transform t = go.transform;
            t.SetParent(visual, false);
            t.localPosition = on.sprite.bounds.center;
            // Upright and one cell big whatever the sprite under it is: hand-made blocks are turned and stretched.
            t.rotation = Quaternion.identity;
            Vector3 scale = t.lossyScale;
            t.localScale = new Vector3(1f / Mathf.Abs(scale.x), 1f / Mathf.Abs(scale.y), 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite;
            sr.sortingLayerID = on.sortingLayerID;
            // Over everything a building draws (cracks, and a hand-made wall's dressing in front of it), under the players.
            sr.sortingOrder = on.sortingOrder + 1;
        }
    }
}
