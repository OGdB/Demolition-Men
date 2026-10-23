using UnityEngine;

namespace Demolition.Run
{
    /// <summary>
    /// Feel, not rules: screen shake scaled to what came down, and a few frames of
    /// hit-stop when a big collapse starts, so weak-point hits land with weight.
    /// </summary>
    public sealed class RunJuice : MonoBehaviour
    {
        [Tooltip("Collapsed cells at which shake and hit-stop reach full strength.")]
        [SerializeField] private int bigCollapse = 40;
        [SerializeField] private float maxCollapseShake = 0.75f;
        [Tooltip("Cells a collapse needs before it freezes time for a moment.")]
        [SerializeField] private int hitStopCells = 12;
        [SerializeField] private float hitStopSeconds = 0.07f;
        [SerializeField, Range(0f, 1f)] private float hitStopTimeScale = 0.05f;

        private float _hitStopUntil = -1f;
        private float _hitStopFrom;

        /// <summary>The time scale a hit-stop returns to (the Clip Lab's slow motion); 1 in play.</summary>
        public static float BaseTimeScale = 1f;

        private void OnEnable()
        {
            DestructionEvents.Collapsed += OnCollapsed;
            DestructionEvents.BlockDestroyed += OnBlockDestroyed;
            Dynamite.Exploded += OnExploded;
        }

        private void OnDisable()
        {
            DestructionEvents.Collapsed -= OnCollapsed;
            DestructionEvents.BlockDestroyed -= OnBlockDestroyed;
            Dynamite.Exploded -= OnExploded;
            EndHitStop();
        }

        private void Update()
        {
            // Also when the clock jumped back (a recorder started mid-stop and changed its base).
            if (_hitStopUntil >= 0f && (CameraShake.Clock >= _hitStopUntil || CameraShake.Clock < _hitStopFrom))
                EndHitStop();
        }

        private void OnCollapsed(CollapseInfo info)
        {
            float strength = Mathf.Clamp01(info.Cells.Count / (float)bigCollapse);
            CameraShake.Add(0.15f + strength * (maxCollapseShake - 0.15f));
            Particles.Burst(info.Position, new Color(0.75f, 0.7f, 0.6f, 0.8f), 10 + info.Cells.Count / 2, 2.5f, 0.35f, 1.1f);
            if (info.Cells.Count >= hitStopCells)
                HitStop(hitStopSeconds * (0.6f + strength));
        }

        private void OnBlockDestroyed(BlockDestroyedInfo info)
        {
            bool heavy = info.Material == BlockMaterial.Support || info.Material == BlockMaterial.Metal;
            CameraShake.Add(heavy ? 0.12f : 0.035f);
        }

        private void OnExploded(Vector3 position) => CameraShake.Add(0.5f);

        private void HitStop(float seconds)
        {
            if (PauseController.MenuOpen)
                return;
            _hitStopFrom = CameraShake.Clock;
            _hitStopUntil = _hitStopFrom + seconds;
            Time.timeScale = hitStopTimeScale * BaseTimeScale;
        }

        private void EndHitStop()
        {
            if (_hitStopUntil < 0f)
                return;
            _hitStopUntil = -1f;
            if (!PauseController.Frozen)
                Time.timeScale = BaseTimeScale;
        }
    }
}
