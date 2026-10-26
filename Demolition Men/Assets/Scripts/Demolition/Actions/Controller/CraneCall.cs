using System.Collections;
using UnityEngine;

namespace Demolition.Actions
{
    /// <summary>The wait between the whistle and the drop: a blinking target where the pallet
    /// will land (picked by the deciding device), then the pallet from above the top of the screen.</summary>
    public sealed class CraneCall : MonoBehaviour
    {
        public void Run(Vector2 pointedAt, Vector2 target, int who, bool authoritative, float delay) =>
            StartCoroutine(Calling(pointedAt, target, who, authoritative, delay));

        /// <summary>Where the pallet lands below the spot the player pointed at.</summary>
        public static Vector2 Pick(Vector2 pointedAt) => ActionStrikes.GroundBelow(pointedAt + Vector2.up * 20f);

        private IEnumerator Calling(Vector2 pointedAt, Vector2 target, int who, bool authoritative, float delay)
        {
            Camera cam = Camera.main; // only how high it falls from, which is each screen's own
            float top = cam != null && cam.orthographic ? cam.transform.position.y + cam.orthographicSize + 2f : pointedAt.y + 16f;
            top = Mathf.Max(top, target.y + 8f);
            float end = Time.time + delay;
            while (Time.time < end)
            {
                ActionStrikes.Ring(target + new Vector2(0f, 0.3f), 2f, new Color(1f, 0.25f, 0.2f, 0.9f), 0.25f);
                yield return new WaitForSeconds(0.2f);
            }
            CranePallet.Drop(new Vector2(pointedAt.x, top), who, authoritative);
            Destroy(gameObject);
        }
    }
}
