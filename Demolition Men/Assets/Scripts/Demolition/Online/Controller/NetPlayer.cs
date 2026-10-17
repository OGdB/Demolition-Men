using Unity.Netcode;
using UnityEngine;

namespace Demolition.Online
{
    /// <summary>
    /// Online part of a player (on the NetPlayer_1/2 prefabs, next to <c>NetworkObject</c> and
    /// an owner-authority <c>NetworkTransform</c>). The owner's device plays it as usual and
    /// publishes its pose and health as a byte each, and its weapon, hits and dynamite; on the
    /// other device it is a proxy (<see cref="PlayerHealth.MakeProxy"/>) whose animator, health
    /// bar and HUD line follow those.
    /// Both devices name it Player_1 (the host's) or Player_2, which the run looks for.
    /// </summary>
    public sealed class NetPlayer : NetworkBehaviour
    {
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int InAirParam = Animator.StringToHash("InAir");
        private static readonly int PunchParam = Animator.StringToHash("IsPunching");
        private static readonly int AxeParam = Animator.StringToHash("IsAxeing");
        private static readonly int WreckParam = Animator.StringToHash("IsWrecking");

        private readonly NetworkVariable<byte> _pose = new NetworkVariable<byte>(writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<byte> _health = new NetworkVariable<byte>(255, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<int> _kit = new NetworkVariable<int>(writePerm: NetworkVariableWritePermission.Owner);

        private PlayerHealth _playerHealth;
        private PlayerCombat _combat;
        private PlayerMotor _motor;
        private SpriteRenderer _sprite;
        private Animator _animator;
        private Vector3 _lastPosition;

        private void Awake()
        {
            _playerHealth = GetComponent<PlayerHealth>();
            _combat = GetComponent<PlayerCombat>();
            _motor = GetComponent<PlayerMotor>();
            _sprite = GetComponentInChildren<SpriteRenderer>();
            _animator = GetComponent<Animator>();
        }

        public override void OnNetworkSpawn()
        {
            gameObject.name = IsOwnedByServer ? "Player_1" : "Player_2";
            _lastPosition = transform.position;
            if (!IsOwner)
                _playerHealth.MakeProxy();
        }

        private void Update()
        {
            if (!IsSpawned)
                return;
            if (IsOwner)
                Publish();
            else
                Follow();
        }

        private void Publish()
        {
            bool punch = _animator != null && _animator.GetBool(PunchParam);
            bool axe = _animator != null && _animator.GetBool(AxeParam);
            bool wreck = _animator != null && _animator.GetBool(WreckParam);
            bool grounded = _animator == null || !_animator.GetBool(InAirParam);
            int weapon = axe ? (int)WeaponType.Axe : wreck ? (int)WeaponType.WreckingBall : (int)WeaponType.Fists;
            _pose.Value = NetPacking.PackPose(_sprite == null || !_sprite.flipX, grounded, punch || axe || wreck, weapon);
            _health.Value = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(_playerHealth.Current / _playerHealth.Max));
            _kit.Value = NetPacking.PackKit((int)_combat.Weapon, _combat.WeaponUses, _combat.DynamiteCount);
        }

        private void Follow()
        {
            NetPacking.UnpackPose(_pose.Value, out bool facingRight, out bool grounded, out bool swinging, out int weapon);
            if (_sprite != null)
                _sprite.flipX = !facingRight;
            if (_animator != null)
            {
                float speed = Time.deltaTime > 0f ? Mathf.Abs(transform.position.x - _lastPosition.x) / Time.deltaTime : 0f;
                _animator.SetFloat(SpeedParam, _motor != null && _motor.MaxSpeed > 0f ? Mathf.Clamp01(speed / _motor.MaxSpeed) : 0f);
                _animator.SetBool(InAirParam, !grounded);
                _animator.SetBool(PunchParam, swinging && weapon == (int)WeaponType.Fists);
                _animator.SetBool(AxeParam, swinging && weapon == (int)WeaponType.Axe);
                _animator.SetBool(WreckParam, swinging && weapon == (int)WeaponType.WreckingBall);
            }
            _lastPosition = transform.position;
            _playerHealth.SetProxyHealth(_health.Value / 255f * _playerHealth.Max);
            NetPacking.UnpackKit(_kit.Value, out int kitWeapon, out int uses, out int dynamite);
            _combat.ShowKit((WeaponType)kitWeapon, uses, dynamite);
        }
    }
}
