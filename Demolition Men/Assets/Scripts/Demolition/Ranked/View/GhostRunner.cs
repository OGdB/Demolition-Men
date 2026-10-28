using Demolition.Run;
using TMPro;
using UnityEngine;

namespace Demolition.Ranked
{
    /// <summary>
    /// The day's best run as a ghost (docs/ranked.md §6): see-through, under the players, running
    /// where its first player ran at the same moment of their run, its name over its head, gone at
    /// its finish. It also lets anyone see how a top run was played. Added by LevelBootstrap in ranked
    /// runs; waits for <see cref="RankedSession.GhostLog"/> to arrive.
    /// </summary>
    public sealed class GhostRunner : MonoBehaviour
    {
        private static readonly Color Tint = new Color(0.65f, 0.9f, 1f, 0.38f);

        private RunController _run;
        private Transform _ghost; // its own object in the scene: the run's stays put
        private SpriteRenderer _sprite;
        private TextMeshPro _name;
        private RunLog _log;

        private void Awake() => _run = GetComponent<RunController>();

        private void LateUpdate()
        {
            if (_log == null)
            {
                if (RankedSession.GhostLog == null || _run == null || _run.Players.Count == 0)
                    return;
                _log = RankedSession.GhostLog;
                Build();
            }
            if (_run.State == null || _run.State.Phase == RunPhase.Countdown || _log.Poses[0].Count == 0)
            {
                _sprite.enabled = _name.enabled = false;
                return;
            }
            float t = _run.State.Elapsed;
            bool there = t <= _log.TimeMs / 1000f;
            _sprite.enabled = _name.enabled = there;
            if (!there)
                return;
            var poses = _log.Poses[0];
            float at = t / RunLog.PoseInterval;
            int i = Mathf.Clamp(Mathf.FloorToInt(at), 0, poses.Count - 1), j = Mathf.Min(i + 1, poses.Count - 1);
            float f = Mathf.Clamp01(at - i);
            _ghost.position = new Vector3(Mathf.Lerp(poses[i].X, poses[j].X, f), Mathf.Lerp(poses[i].Y, poses[j].Y, f), 0f);
            _sprite.flipX = poses[i].Left;
        }

        // The first player's look at the start, washed out; its renderer's offset kept so the feet line up.
        private void Build()
        {
            SpriteRenderer look = _run.Players[0].Transform.GetComponentInChildren<SpriteRenderer>();
            _ghost = new GameObject("Ghost").transform;
            var body = new GameObject("Body");
            body.transform.SetParent(_ghost, false);
            _sprite = body.AddComponent<SpriteRenderer>();
            if (look != null)
            {
                _sprite.sprite = look.sprite;
                _sprite.sortingLayerID = look.sortingLayerID;
                _sprite.sortingOrder = look.sortingOrder - 1;
                body.transform.localPosition = look.transform.position - _run.Players[0].Transform.position;
                body.transform.localScale = look.transform.lossyScale;
            }
            _sprite.color = Tint;

            var label = new GameObject("Name");
            label.transform.SetParent(_ghost, false);
            label.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            _name = label.AddComponent<TextMeshPro>();
            if (UiFactory.DisplayFont != null)
                _name.font = UiFactory.DisplayFont;
            _name.fontSize = 3f;
            _name.alignment = TextAlignmentOptions.Center;
            _name.color = Tint;
            _name.text = RankedSession.Ghost != null ? RankedSession.Ghost.name : "GHOST";
            _name.rectTransform.sizeDelta = new Vector2(8f, 1f);
            _name.sortingOrder = _sprite.sortingOrder;
        }
    }
}
