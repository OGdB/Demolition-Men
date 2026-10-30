using Demolition.Actions;
using Demolition.Run;
using TMPro;
using UnityEngine;

namespace Demolition.Ranked
{
    /// <summary>
    /// The day's best run as a ghost (docs/ranked.md §6): see-through, under the players, running
    /// where its first player ran at the same moment of their run, its name over its head, gone at
    /// its finish. It also lets anyone see how a top run was played. Its animator is the player's, and
    /// plays what the log tells: running and jumping from the poses, swings from the hits, an
    /// action's frames from its start. Added by LevelBootstrap in ranked runs; waits for
    /// <see cref="RankedSession.GhostLog"/> to arrive.
    /// </summary>
    public sealed class GhostRunner : MonoBehaviour
    {
        private static readonly Color Tint = new Color(0.65f, 0.9f, 1f, 0.38f);
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int InAirParam = Animator.StringToHash("InAir");
        private static readonly int PunchParam = Animator.StringToHash("IsPunching");
        private static readonly int AxeParam = Animator.StringToHash("IsAxeing");
        private static readonly int WreckParam = Animator.StringToHash("IsWrecking");
        /// <summary>A swing starts this long before its hit lands, and goes on this long after.</summary>
        private const float SwingLead = 0.15f, SwingTail = 0.25f;
        /// <summary>Moving up or down this much between two poses is in the air (poses are in 1/32 units).</summary>
        private const float Airborne = 0.06f;

        private RunController _run;
        private Transform _ghost; // its own object in the scene: the run's stays put
        private SpriteRenderer _sprite;
        private TextMeshPro _name;
        private Animator _anim;
        private RunLog _log;
        private int _nextEvent;
        private HitCause _swing;
        private float _swingUntil = float.NegativeInfinity;
        private ActionInfo _action;
        private Sprite[] _actionFrames;
        private float _actionFrom, _actionStrike;

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
            Animate(t, i, j);
        }

        private void Animate(float t, int i, int j)
        {
            Follow(t);
            Sprite frame = ActionFrame(t);
            if (_anim == null)
                return;
            _anim.enabled = frame == null;
            if (frame != null)
            {
                _sprite.sprite = frame; // the animator would set its own every frame
                return;
            }
            var poses = _log.Poses[0];
            _anim.SetFloat(SpeedParam, Mathf.Abs(poses[j].X - poses[i].X) / RunLog.PoseInterval);
            _anim.SetBool(InAirParam, Moving(i - 1) || Moving(i) || Moving(j));
            bool swinging = t <= _swingUntil;
            _anim.SetBool(PunchParam, swinging && _swing == HitCause.Fist);
            _anim.SetBool(AxeParam, swinging && _swing == HitCause.Axe);
            _anim.SetBool(WreckParam, swinging && _swing == HitCause.WreckingBall);
        }

        private bool Moving(int pose)
        {
            var poses = _log.Poses[0];
            return pose >= 0 && pose + 1 < poses.Count && Mathf.Abs(poses[pose + 1].Y - poses[pose].Y) > Airborne;
        }

        // The log's events up to a swing's lead ahead of now: what the ghost should be doing.
        private void Follow(float t)
        {
            for (; _nextEvent < _log.Events.Count && _log.Events[_nextEvent].Time <= t + SwingLead; _nextEvent++)
            {
                LogEvent e = _log.Events[_nextEvent];
                if (e.Player != 0)
                    continue;
                if (e.Kind == LogKind.Damage && (e.Cause == HitCause.Fist || e.Cause == HitCause.Axe || e.Cause == HitCause.WreckingBall))
                {
                    _swing = e.Cause;
                    _swingUntil = e.Time + SwingTail;
                }
                else if (e.Kind == LogKind.ActionStart)
                {
                    _action = ActionCatalog.Get(e.Action);
                    _actionFrames = ActionSprites.Frames(_action, false);
                    _actionFrom = e.Time;
                    _actionStrike = StrikeAfter(_nextEvent);
                }
            }
        }

        private float StrikeAfter(int start)
        {
            for (int k = start + 1; k < _log.Events.Count; k++)
                if (_log.Events[k].Player == 0 && _log.Events[k].Kind == LogKind.ActionStrike)
                    return _log.Events[k].Time;
            return _log.Events[start].Time;
        }

        // The action's frame at t as ActionUser plays them, or null when none plays. A frame held until
        // the player lands lasts until the strike the log has.
        private Sprite ActionFrame(float t)
        {
            if (_action == null || t < _actionFrom)
                return null;
            float at = _actionFrom;
            for (int f = 0; f < _action.Frames.Length; f++)
            {
                at += _action.Frames[f] < 0f ? Mathf.Max(0f, _actionStrike - at) : _action.Frames[f];
                if (t < at || (f == _action.Frames.Length - 1 && t < at + _action.Hold))
                    return _actionFrames[f];
            }
            _action = null;
            return null;
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
            Animator animator = _run.Players[0].Transform.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                body.AddComponent<GhostBody>(); // the clips' events need a receiver
                _anim = body.AddComponent<Animator>();
                _anim.runtimeAnimatorController = animator.runtimeAnimatorController;
            }

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
