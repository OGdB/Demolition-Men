using UnityEngine;

namespace Demolition.Actions
{
    /// <summary>
    /// The burp's shock wave: rolls out from the player's sign along a widening band at chest
    /// height. Every standing block it passes over comes loose whatever it's made of (destroyed,
    /// so what stood on it detaches and falls), and the debris in front is blown away with it.
    /// </summary>
    public sealed class BurpWave : MonoBehaviour
    {
        private const float Range = 7f;
        private const float Speed = 16f;
        private const float HalfHeight = 1f, Spread = 0.12f; // band half-height at the mouth, growth per unit
        // The band's middle sits this far under the mouth: its bottom clears the feet for the
        // first unit or so, so the roof the player stands on stays, and dips lower further out.
        private const float BandDrop = 0.35f;
        private static readonly Color Gas = new Color(0.78f, 0.92f, 0.42f, 0.6f);

        private Vector2 _origin;
        private int _facing, _who;
        private bool _authoritative;
        private float _front, _nextRing;

        public static void Spawn(Vector2 mouth, int facing, int who, bool authoritative)
        {
            var go = new GameObject("BurpWave");
            go.transform.position = mouth;
            BurpWave wave = go.AddComponent<BurpWave>();
            wave._origin = mouth;
            wave._facing = facing;
            wave._who = who;
            wave._authoritative = authoritative;
        }

        private void FixedUpdate()
        {
            float from = _front;
            _front = Mathf.Min(Range, _front + Speed * Time.fixedDeltaTime);
            float half = HalfHeight + Spread * _front;
            var centre = new Vector2(_origin.x + _facing * (from + _front) * 0.5f, _origin.y - BandDrop);
            var size = new Vector2(Mathf.Max(0.1f, _front - from), half * 2f);
            ActionStrikes.Hit(ActionStrikes.BlocksInBox(centre, size), ActionStrikes.Wreck, _who, _authoritative);
            foreach (BuildingBlock block in ActionStrikes.BlocksInBox(centre + new Vector2(_facing * 0.5f, 0.5f), size + new Vector2(1f, 2f)))
                if (block != null && ActionStrikes.IsLoose(block))
                    block.Shove(new Vector2(_facing * 9f, 3f));

            if (Time.time >= _nextRing)
            {
                _nextRing = Time.time + 0.05f;
                Vector2 at = new Vector2(_origin.x + _facing * _front, _origin.y - BandDrop);
                ActionStrikes.Ring(at, half * 2.2f, Gas, 0.35f);
                Particles.Burst(at, Gas, 4, 2.5f, 0.18f, 0.5f);
            }
            if (_front >= Range)
                Destroy(gameObject);
        }
    }
}
