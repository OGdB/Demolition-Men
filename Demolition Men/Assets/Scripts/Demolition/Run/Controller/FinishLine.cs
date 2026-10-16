using UnityEngine;

namespace Demolition.Run
{
    /// <summary>Marks the level's finish: a player whose x passes this transform's x has crossed.</summary>
    public sealed class FinishLine : MonoBehaviour
    {
        /// <summary>The finish in play, if its level part has spawned yet.</summary>
        public static FinishLine Active { get; private set; }

        public float X => transform.position.x;

        private void OnEnable() => Active = this;

        private void OnDisable()
        {
            if (Active == this)
                Active = null;
        }
    }
}
