using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Run
{
    /// <summary>A hint shown once, when the front player passes this transform's x.</summary>
    public sealed class TutorialPrompt : MonoBehaviour
    {
        private static readonly List<TutorialPrompt> _all = new List<TutorialPrompt>();

        [TextArea] [SerializeField] private string message;
        [SerializeField] private float duration = 6f;

        public static IReadOnlyList<TutorialPrompt> All => _all;
        public string Message => message;
        public float Duration => duration;
        public float X => transform.position.x;
        public bool Shown { get; set; }

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);
    }
}
