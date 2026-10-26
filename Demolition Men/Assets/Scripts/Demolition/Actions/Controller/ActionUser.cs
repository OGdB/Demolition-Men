using System.Collections;
using System.Collections.Generic;
using Demolition.Run;
using UnityEngine;

namespace Demolition.Actions
{
    /// <summary>
    /// One player's action: fills its <see cref="ActionMeter"/> from the destruction this player
    /// is credited with, fires it on the action button once full, plays its frames over the
    /// player's animator (controls off meanwhile) and sets its strike off at the strike frame
    /// (<see cref="ActionStrikes"/>). Buffs the action gives live here too, since they change
    /// this player's motor, combat and health.
    ///
    /// Online each device fires its own player's action and tells the other (<see cref="NetRole.ActionEvent"/>):
    /// the other device plays the frames on its copy and runs the same strike. Only the host's
    /// (or an offline) strike damages standing blocks; a client's is the show, and the host's
    /// arrives as cells. Debris is local, so both shove and break their own.
    /// </summary>
    public sealed class ActionUser : MonoBehaviour
    {
        /// <summary>What a device tells the other about its player's action.</summary>
        public const int Announce = 0, Started = 1, Struck = 2;

        private const float AnnounceEvery = 5f;
        private const float SlamUp = 15f, SlamDown = 26f;

        private enum Slam { None, Rising, Falling }

        private PlayerInputReader _input;
        private PlayerMotor _motor;
        private PlayerHealth _health;
        private PlayerCombat _combat;
        private Rigidbody2D _rb;
        private Animator _anim;
        private SpriteRenderer _sr;
        private bool _player2Look;
        private Coroutine _playing;
        private bool _landed;
        private Slam _slam;
        private float _slamFrom;
        private float _announceAt;
        private float _quietUntil;
        private float _coffeeUntil, _vestUntil, _boostUntil;
        private readonly HashSet<TutorialPrompt> _charged = new HashSet<TutorialPrompt>();

        public ActionInfo Info { get; private set; }
        public ActionMeter Meter { get; private set; }
        public int PlayerIndex { get; private set; }
        /// <summary>Playing its frames (the controls are off).</summary>
        public bool Busy => _playing != null;
        /// <summary>The other device's player online: the network moves it, its device fires its action.</summary>
        public bool IsProxy => _health != null && _health.IsProxy;
        public int Facing => _sr != null && _sr.flipX ? -1 : 1;
        public SpriteRenderer Sprite => _sr;
        public Animator Animator => _anim;
        public PlayerCombat Combat => _combat;

        public void Configure(int index, ActionKind kind)
        {
            PlayerIndex = index;
            SetKind(kind);
        }

        // A different action (online, the other device's loadout): the meter keeps its share.
        private void SetKind(ActionKind kind)
        {
            float share = Meter != null ? Meter.Fraction : 0f;
            Info = ActionCatalog.Get(kind);
            Meter = new ActionMeter(Info.Charge);
            Meter.Add(share * Meter.Capacity);
        }

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _motor = GetComponent<PlayerMotor>();
            _health = GetComponent<PlayerHealth>();
            _combat = GetComponent<PlayerCombat>();
            _rb = GetComponent<Rigidbody2D>();
            _anim = GetComponent<Animator>();
            _sr = GetComponentInChildren<SpriteRenderer>();
            _player2Look = gameObject.name == "Player_2";
            if (Info == null)
                SetKind(ActionCatalog.Default);
        }

        private void OnEnable()
        {
            DestructionEvents.BlockDestroyed += OnBlockDestroyed;
            DestructionEvents.Collapsed += OnCollapsed;
        }

        private void OnDisable()
        {
            DestructionEvents.BlockDestroyed -= OnBlockDestroyed;
            DestructionEvents.Collapsed -= OnCollapsed;
            if (_playing != null)
            {
                StopCoroutine(_playing);
                EndFrames(local: !IsProxy);
            }
        }

        private static bool Playing
        {
            get
            {
                RunController run = RunController.Current;
                return run == null || run.State.Phase == RunPhase.Playing;
            }
        }

        // The meter fills from this player's demolition, but not from what its own action breaks.
        private bool Charging => Playing && !Busy && Time.time >= _quietUntil;

        private void OnBlockDestroyed(BlockDestroyedInfo info)
        {
            if (info.Instigator == PlayerIndex && Charging)
                Meter.Add(ActionMeter.PointsFor(info.Cells, collapse: false));
        }

        private void OnCollapsed(CollapseInfo info)
        {
            if (info.Instigator == PlayerIndex && Charging)
                Meter.Add(ActionMeter.PointsFor(info.Cells.Count, collapse: true));
        }

        private void Update()
        {
            bool playing = Playing;
            if (playing && !Busy)
                Meter.Tick(Time.deltaTime);
            if (IsProxy || _input == null)
                return;

            if (NetRole.IsOnline && Time.time >= _announceAt)
            {
                _announceAt = Time.time + AnnounceEvery; // the other device may not have been listening yet
                NetRole.ActionEvent?.Invoke(Announce, PlayerIndex, (int)Info.Kind, transform.position, Facing);
            }

            foreach (TutorialPrompt prompt in TutorialPrompt.All)
                if (prompt.ChargesAction && transform.position.x >= prompt.Position.x && _charged.Add(prompt))
                    Meter.Fill();

            if (playing && !Busy && !_health.IsDead && _input.enabled && _input.ActionDown && Meter.TrySpend())
            {
                NetRole.ActionEvent?.Invoke(Started, PlayerIndex, (int)Info.Kind, transform.position, Facing);
                _playing = StartCoroutine(Play(local: true));
            }
        }

        /// <summary>Online: what the other device says its player (this copy) did.</summary>
        public void OnRemote(int phase, ActionKind kind, Vector2 at, int facing)
        {
            if (Info.Kind != kind)
                SetKind(kind);
            switch (phase)
            {
                case Started:
                    Meter.Empty();
                    if (_playing == null)
                        _playing = StartCoroutine(Play(local: false));
                    break;
                case Struck:
                    _landed = true;
                    _quietUntil = Time.time + Quiet(Info);
                    ActionStrikes.Run(this, kind, at, facing, authoritative: NetRole.IsHost);
                    break;
            }
        }

        private static float Quiet(ActionInfo info) => info.Duration > 0f ? info.Duration : 2.5f;

        // ---------------- Frames ----------------

        private IEnumerator Play(bool local)
        {
            ActionInfo info = Info;
            Sprite[] frames = ActionSprites.Frames(info, _player2Look);
            bool show = _anim != null && _sr != null && frames.Length > 0 && frames[0] != null;
            if (local)
                _input.Suppressed = true;
            if (show)
                _anim.enabled = false; // it would set its own sprite every frame
            _landed = false;
            if (local)
                Begin(info);

            float holdUntil = Time.time + info.Hold;
            bool struck = false;
            do
            {
                for (int f = 0; f < info.Frames.Length && !_health.IsDead; f++)
                {
                    if (!struck && f == info.StrikeFrame)
                    {
                        struck = true;
                        if (local)
                            Strike(info);
                    }
                    float seconds = info.Frames[f];
                    float end = Time.time + (seconds == ActionInfo.UntilLanded ? 3f : seconds); // 3 s: fell into a pit
                    while (Time.time < end && !_health.IsDead && !(seconds == ActionInfo.UntilLanded && _landed))
                    {
                        if (show && frames[f] != null)
                            _sr.sprite = frames[f];
                        yield return null;
                    }
                }
            }
            while (Time.time < holdUntil && !_health.IsDead);

            EndFrames(local);
        }

        private void EndFrames(bool local)
        {
            if (_anim != null)
                _anim.enabled = true;
            if (local && _input != null)
                _input.Suppressed = false;
            _slam = Slam.None;
            _playing = null;
        }

        // The moment the button goes: the slam's hop, the sip, the whistle.
        private void Begin(ActionInfo info)
        {
            switch (info.Kind)
            {
                case ActionKind.ButtSlam:
                    _motor.Knockback(new Vector2(_rb.linearVelocity.x * 0.3f, SlamUp)); // a stagger: no low-jump cut
                    _slam = Slam.Rising;
                    break;
                case ActionKind.Burp:
                case ActionKind.CoffeeBreak:
                    ActionAudio.Slurp();
                    break;
                case ActionKind.CraneDrop:
                    ActionAudio.Whistle();
                    break;
            }
        }

        private void Strike(ActionInfo info)
        {
            Vector2 at = StrikePoint(info.Kind);
            _quietUntil = Time.time + Quiet(info);
            NetRole.ActionEvent?.Invoke(Struck, PlayerIndex, (int)info.Kind, at, Facing);
            ActionStrikes.Run(this, info.Kind, at, Facing, authoritative: !NetRole.IsClient);
        }

        private Vector2 StrikePoint(ActionKind kind)
        {
            Vector2 feet = transform.position;
            switch (kind)
            {
                case ActionKind.Burp: return feet + new Vector2(0.7f * Facing, 1.5f);
                case ActionKind.CraneDrop: return feet + new Vector2(5f * Facing, 0f);
                case ActionKind.SiteRadio: return feet + new Vector2(0.85f * Facing, 0f);
                default: return feet;
            }
        }

        // ---------------- The slam ----------------

        private void FixedUpdate()
        {
            if (_slam == Slam.Rising && _rb.linearVelocity.y < 1f)
            {
                _slam = Slam.Falling;
                _slamFrom = Time.time;
                _rb.linearVelocity = new Vector2(0f, -SlamDown);
            }
            else if (_slam == Slam.Falling)
            {
                if (_rb.linearVelocity.y > -SlamDown * 0.6f && Time.time - _slamFrom < 0.1f)
                    _rb.linearVelocity = new Vector2(0f, -SlamDown);
                else if (Time.time - _slamFrom > 0.1f && _rb.linearVelocity.y > -0.5f)
                    Landed(); // stopped on something the contacts didn't report
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_slam == Slam.Falling)
                for (int i = 0; i < collision.contactCount; i++)
                    if (collision.GetContact(i).normal.y > 0.5f)
                        Landed();

            // The vest: falling rubble bounces off the hard hat.
            if (Time.time < _vestUntil)
            {
                var block = collision.collider.GetComponentInParent<BuildingBlock>();
                if (block != null && block.IsFalling)
                {
                    Vector2 away = (Vector2)(block.transform.position - transform.position);
                    block.Shove(away.normalized * 4f + Vector2.up * 5f);
                    Particles.Burst(collision.GetContact(0).point, new Color(1f, 0.6f, 0.15f), 6, 3f, 0.1f, 0.4f);
                }
            }
        }

        private void Landed()
        {
            _slam = Slam.None;
            _landed = true;
        }

        // ---------------- Buffs ----------------

        /// <summary>The coffee: faster, harder, quicker swings, steaming.</summary>
        public void Coffee(float seconds)
        {
            bool start = Time.time >= _coffeeUntil;
            _coffeeUntil = Time.time + seconds;
            ActionGlow.Show(gameObject, new Color(0.55f, 0.32f, 0.15f, 0.9f), seconds);
            if (start)
                StartCoroutine(CoffeeRoutine());
        }

        private IEnumerator CoffeeRoutine()
        {
            const float speed = 1.35f, damage = 1.5f, swing = 1.5f;
            bool local = !IsProxy;
            if (local)
            {
                _motor.MaxSpeed *= speed;
                _combat.DamageMultiplier *= damage;
            }
            if (_anim != null)
                _anim.speed *= swing;
            float puff = 0f;
            while (Time.time < _coffeeUntil && !_health.IsDead)
            {
                if ((puff -= Time.deltaTime) <= 0f)
                {
                    puff = 0.18f;
                    FxRise.Spawn(PrimitiveSprite.Disc(), transform.position + new Vector3(Random.Range(-0.2f, 0.2f), 1.9f),
                        new Color(1f, 1f, 1f, 0.55f), 0.22f, 0.9f, new Vector2(0f, 1.2f), wobble: 0.5f, grow: 0.35f);
                }
                yield return null;
            }
            if (local)
            {
                _motor.MaxSpeed /= speed;
                _combat.DamageMultiplier /= damage;
            }
            if (_anim != null)
                _anim.speed /= swing;
        }

        /// <summary>The hi-vis vest: nothing hurts, rubble bounces off.</summary>
        public void Vest(float seconds)
        {
            bool start = Time.time >= _vestUntil;
            _vestUntil = Time.time + seconds;
            ActionGlow.Show(gameObject, new Color(1f, 0.55f, 0.08f, 1f), seconds);
            if (start && !IsProxy)
                StartCoroutine(VestRoutine());
        }

        private IEnumerator VestRoutine()
        {
            bool was = _health.Invincible; // the Clip Lab's bots are invincible anyway
            _health.Invincible = true;
            while (Time.time < _vestUntil)
                yield return null;
            _health.Invincible = was;
        }

        /// <summary>Leaning on the shovel: heal while the lean lasts.</summary>
        public void Rest(float seconds, float perSecond)
        {
            if (!IsProxy)
                StartCoroutine(RestRoutine(seconds, perSecond));
        }

        private IEnumerator RestRoutine(float seconds, float perSecond)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !_health.IsDead)
            {
                _health.Heal(perSecond * Time.deltaTime);
                yield return null;
            }
        }

        /// <summary>The partner of someone leaning on a shovel: hits twice as hard for a while.</summary>
        public void Boost(float seconds)
        {
            bool start = Time.time >= _boostUntil;
            _boostUntil = Time.time + seconds;
            ActionGlow.Show(gameObject, new Color(1f, 0.85f, 0.25f, 1f), seconds);
            if (start && !IsProxy)
                StartCoroutine(BoostRoutine());
        }

        private IEnumerator BoostRoutine()
        {
            const float damage = 2f;
            _combat.DamageMultiplier *= damage;
            while (Time.time < _boostUntil && !_health.IsDead)
                yield return null;
            _combat.DamageMultiplier /= damage;
        }
    }
}
