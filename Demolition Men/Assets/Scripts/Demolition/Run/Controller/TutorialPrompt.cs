using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Run
{
    /// <summary>
    /// Where a tutorial hint's stretch of the level starts, on the ground. Its sign travels
    /// with the front player from here to the next marker and then stays standing there
    /// (<see cref="TutorialTrail"/>, drawn by <see cref="TutorialSigns"/>).
    /// </summary>
    public sealed class TutorialPrompt : MonoBehaviour
    {
        private static readonly List<TutorialPrompt> _all = new List<TutorialPrompt>();

        [Tooltip("First line: the sign's heading. {MOVE} {JUMP} {HIT} {TNT} {SWAP} become the buttons of the devices in use.")]
        [TextArea(2, 5)] [SerializeField] private string message;

        public static IReadOnlyList<TutorialPrompt> All => _all;
        public string Message => message;
        public Vector2 Position => transform.position;

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);
    }
}
