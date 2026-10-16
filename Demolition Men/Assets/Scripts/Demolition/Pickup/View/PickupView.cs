using UnityEngine;

namespace Demolition
{
    /// <summary>Shows the pickup's icon on a bobbing, pulsing child sprite.</summary>
    [RequireComponent(typeof(Pickup))]
    public sealed class PickupView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer icon;
        [SerializeField] private Sprite axe;
        [SerializeField] private Sprite wreckingBall;
        [SerializeField] private Sprite dynamite;
        [SerializeField] private Sprite resurrect;
        [SerializeField] private float bobHeight = 0.15f;
        [SerializeField] private float bobSpeed = 3f;

        private Pickup _pickup;
        private Vector3 _iconRest;

        private void Start()
        {
            _pickup = GetComponent<Pickup>();
            _iconRest = icon.transform.localPosition;
            icon.sprite = SpriteFor(_pickup.Kind);
        }

        private void Update()
        {
            float t = Time.time * bobSpeed;
            icon.transform.localPosition = _iconRest + Vector3.up * (Mathf.Sin(t) * bobHeight);
            icon.transform.localScale = Vector3.one * (1f + Mathf.Sin(t * 2f) * 0.05f);
        }

        private Sprite SpriteFor(PickupKind kind)
        {
            switch (kind)
            {
                case PickupKind.Axe: return axe;
                case PickupKind.WreckingBall: return wreckingBall;
                case PickupKind.Resurrect: return resurrect;
                default: return dynamite;
            }
        }
    }
}
