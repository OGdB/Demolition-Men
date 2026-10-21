using System.Collections.Generic;
using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// A chain or plank walkway of hinged links, outside the support graph like the crane:
    /// the child links become dynamic bodies that sag and swing under the players. Each end
    /// hangs from an anchor body (a pillar's block) or a pin in the world. A link keeps its
    /// <see cref="BuildingBlock"/> unregistered, so hits and blasts still break it and score;
    /// the chain parts there, and an end whose anchor block is destroyed drops.
    /// </summary>
    public class HangingChain : MonoBehaviour
    {
        [Tooltip("What the first link (leftmost; lowest for an upright chain) hangs from.")]
        [SerializeField] private Rigidbody2D startAnchor;
        [Tooltip("Without a start anchor: pin the first link to the world instead of leaving it loose.")]
        [SerializeField] private bool pinStart;
        [Tooltip("What the last link (rightmost; highest for an upright chain) hangs from.")]
        [SerializeField] private Rigidbody2D endAnchor;
        [Tooltip("Without an end anchor: pin the last link to the world instead of leaving it loose.")]
        [SerializeField] private bool pinEnd;

        // Every joint and the body it hangs from. A destroyed body would leave its joint pinned
        // to the world where the body was, so such joints are removed instead.
        private readonly List<(HingeJoint2D joint, Rigidbody2D body)> _joints = new List<(HingeJoint2D, Rigidbody2D)>();

        private void Awake()
        {
            // Inside a building, its scan at Start would adopt the links as standing blocks.
            var building = GetComponentInParent<DestructibleBuilding>();
            if (building != null)
                transform.SetParent(building.transform.parent, true);
        }

        // Start, not Awake: BuildingBlock.Awake makes every link static first.
        private void Start()
        {
            var links = new List<Rigidbody2D>(GetComponentsInChildren<Rigidbody2D>());
            if (links.Count == 0)
                return;
            Order(links);
            Physics2D.SyncTransforms();

            for (int i = 1; i < links.Count; i++)
                Hinge(links[i], links[i - 1]);
            End(links, 0, startAnchor, pinStart);
            End(links, links.Count - 1, endAnchor, pinEnd);
            foreach (Rigidbody2D link in links)
                link.bodyType = RigidbodyType2D.Dynamic;
        }

        private void FixedUpdate()
        {
            for (int i = _joints.Count - 1; i >= 0; i--)
            {
                (HingeJoint2D joint, Rigidbody2D body) = _joints[i];
                if (joint != null && body != null)
                    continue;
                if (joint != null)
                {
                    joint.enabled = false; // now: Destroy only lands after this physics step
                    Destroy(joint);
                }
                _joints.RemoveAt(i);
            }
        }

        // Along the chain: by x, or by y when it runs more up than across.
        private static void Order(List<Rigidbody2D> links)
        {
            Vector2 min = links[0].transform.position, max = min;
            foreach (Rigidbody2D link in links)
            {
                min = Vector2.Min(min, link.transform.position);
                max = Vector2.Max(max, link.transform.position);
            }
            bool across = max.x - min.x >= max.y - min.y;
            links.Sort((a, b) => across
                ? a.transform.position.x.CompareTo(b.transform.position.x)
                : a.transform.position.y.CompareTo(b.transform.position.y));
        }

        private void End(List<Rigidbody2D> links, int index, Rigidbody2D anchor, bool pin)
        {
            Rigidbody2D link = links[index];
            if (anchor != null)
            {
                Hinge(link, anchor);
            }
            else if (pin && links.Count > 1)
            {
                // The link's outer end: half a link past its centre, away from its neighbour.
                Vector2 neighbour = links[index == 0 ? 1 : index - 1].transform.position;
                Vector2 centre = link.transform.position;
                Vector2 at = centre + (centre - neighbour) * 0.5f;
                var joint = link.gameObject.AddComponent<HingeJoint2D>();
                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = link.transform.InverseTransformPoint(at);
                joint.connectedAnchor = at; // no connected body: a point in the world
            }
        }

        // Hinged where the two touch.
        private void Hinge(Rigidbody2D link, Rigidbody2D other)
        {
            var a = link.GetComponent<Collider2D>();
            var b = other.GetComponent<Collider2D>();
            Vector2 at = a != null && b != null
                ? Midpoint(Physics2D.Distance(a, b))
                : ((Vector2)link.transform.position + (Vector2)other.transform.position) * 0.5f;
            var joint = link.gameObject.AddComponent<HingeJoint2D>();
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedBody = other;
            joint.anchor = link.transform.InverseTransformPoint(at);
            joint.connectedAnchor = other.transform.InverseTransformPoint(at);
            _joints.Add((joint, other));
        }

        private static Vector2 Midpoint(ColliderDistance2D d) => (d.pointA + d.pointB) * 0.5f;
    }
}
