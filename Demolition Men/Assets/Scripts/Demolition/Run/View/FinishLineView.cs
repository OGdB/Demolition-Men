using UnityEngine;

namespace Demolition.Run
{
    /// <summary>Checkered banner standing on the finish line.</summary>
    [RequireComponent(typeof(FinishLine))]
    public sealed class FinishLineView : MonoBehaviour
    {
        [SerializeField] private Vector2 size = new Vector2(1f, 8f);
        [SerializeField] private int sortingOrder = -5;

        private void Awake()
        {
            var visual = new GameObject("Banner");
            visual.transform.SetParent(transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = PrimitiveSprite.Checker();
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = size;
            sr.sortingOrder = sortingOrder;
        }
    }
}
