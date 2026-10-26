using UnityEngine;

namespace Demolition.Actions
{
    /// <summary>
    /// The site radio, turned all the way up: it stands where it was put down and pulses with
    /// the beat. Each pulse bursts every pane in reach, rattles the standing walls (a little
    /// damage, so brick and wood give way over the song) and makes the rubble around it jump.
    /// </summary>
    public sealed class SiteRadio : MonoBehaviour
    {
        private const float Reach = 6f;
        private const float Beat = 0.5f;
        private const float Rattle = 10f; // damage per beat to a standing wall in reach
        private const int MaxJumps = 40; // rubble kicked per beat, so a big heap stays cheap

        private SpriteRenderer _sr;
        private int _who;
        private bool _authoritative;
        private float _showAt, _end, _nextBeat;
        private int _step;

        /// <param name="showAfter">Seconds until it appears: the player's own frames hold the radio until then.</param>
        public static void Place(Vector2 at, int facing, int who, float seconds, float showAfter, bool authoritative)
        {
            var go = new GameObject("SiteRadio");
            go.transform.position = ActionStrikes.GroundBelow(at + Vector2.up);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ActionSprites.Prop("radio");
            sr.flipX = facing < 0;
            sr.sortingOrder = 11;
            sr.enabled = false;
            SiteRadio radio = go.AddComponent<SiteRadio>();
            radio._sr = sr;
            radio._who = who;
            radio._authoritative = authoritative;
            radio._showAt = Time.time + showAfter;
            radio._nextBeat = radio._showAt;
            radio._end = radio._showAt + seconds;
        }

        private void Update()
        {
            float now = Time.time;
            if (now < _showAt)
                return;
            if (!_sr.enabled)
                ActionAudio.Static(); // switched on
            _sr.enabled = true;
            float bump = Mathf.Clamp01((_nextBeat - now) / Beat);
            transform.localScale = new Vector3(1f + 0.08f * bump, 1f + 0.12f * bump, 1f); // thumps with the beat
            if (now >= _end)
            {
                Particles.Burst(transform.position + Vector3.up * 0.3f, new Color(1f, 0.75f, 0.2f), 14, 3f, 0.14f, 0.6f);
                Destroy(gameObject);
                return;
            }
            if (now >= _nextBeat)
            {
                _nextBeat += Beat;
                Pulse();
            }
        }

        private void Pulse()
        {
            Vector2 at = (Vector2)transform.position + Vector2.up * 0.4f;
            ActionAudio.Beat(_step);
            ActionStrikes.Ring(at, Reach * 2f, new Color(1f, 0.85f, 0.35f, 0.35f), 0.45f);
            FxRise.Spawn(ActionSprites.Prop("note"), at + new Vector2(Random.Range(-0.4f, 0.4f), 0.4f),
                Color.HSVToRGB((_step * 0.17f) % 1f, 0.6f, 1f), 1f, 1.2f, new Vector2(Random.Range(-0.6f, 0.6f), 1.6f), wobble: 0.8f);
            _step++;

            int jumps = 0;
            foreach (BuildingBlock block in ActionStrikes.BlocksInCircle(at, Reach))
            {
                if (block == null)
                    continue;
                bool loose = ActionStrikes.IsLoose(block);
                if (block.Material == BlockMaterial.Glass)
                {
                    if (loose || _authoritative)
                        block.Shatter(_who);
                }
                else if (loose)
                {
                    if (jumps++ < MaxJumps)
                        block.Shove(new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(2f, 4f)));
                }
                else if (_authoritative)
                {
                    block.TakeDamage(Rattle, _who);
                }
            }
        }
    }
}
