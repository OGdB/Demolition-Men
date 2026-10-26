using System.Collections;
using UnityEngine;

namespace Demolition.Actions
{
    /// <summary>The wait between the whistle and the drop: a blinking target where the pallet
    /// will land, then the pallet from above the top of the screen.</summary>
    public sealed class CraneCall : MonoBehaviour
    {
        public void Run(Vector2 pointedAt, int who, bool authoritative, float delay) =>
            StartCoroutine(Calling(pointedAt, who, authoritative, delay));

        private IEnumerator Calling(Vector2 pointedAt, int who, bool authoritative, float delay)
        {
            Camera cam = Camera.main;
            float top = cam != null && cam.orthographic ? cam.transform.position.y + cam.orthographicSize + 2f : pointedAt.y + 16f;
            top = Mathf.Max(top, pointedAt.y + 8f);
            Vector2 target = ActionStrikes.GroundBelow(new Vector2(pointedAt.x, top));
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
