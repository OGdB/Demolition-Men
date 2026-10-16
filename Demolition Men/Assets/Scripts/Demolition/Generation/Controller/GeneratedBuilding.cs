using UnityEngine;

namespace Demolition.Generation
{
    /// <summary>A spot that grows a generated building at Start: pick one of the archetypes,
    /// generate a validated spec from the seed, build it standing on this transform. It
    /// never grows into a building that is already standing to its right.</summary>
    public sealed class GeneratedBuilding : MonoBehaviour
    {
        private const int MinWidth = 5;
        private const float ScanHeight = 40f;

        [SerializeField] private TextAsset[] archetypes = new TextAsset[0];
        [SerializeField] private BuildingTileset tileset;
        [SerializeField] private int seed;
        [Tooltip("Ignore the seed and roll a new building every time.")]
        [SerializeField] private bool randomSeed = true;
        [Tooltip("Widest building this spot has room for, in cells; 0 = the archetype's own range.")]
        [SerializeField] private int maxWidth;

        private void Start()
        {
            if (archetypes.Length == 0)
                return;
            int s = randomSeed ? Random.Range(int.MinValue, int.MaxValue) : seed;
            var rng = new Pcg32(s);
            BuildingArchetype archetype = BuildingArchetype.FromJson(archetypes[rng.Range(0, archetypes.Length - 1)].text);

            int room = FreeWidth(maxWidth > 0 ? maxWidth : archetype.width.max);
            if (room < MinWidth)
                return; // another building already stands here
            archetype.width.max = Mathf.Min(archetype.width.max, room);
            archetype.width.min = Mathf.Min(archetype.width.min, archetype.width.max);

            BuildingSpec spec = BuildingGrammar.GenerateValid(archetype, s, out _);
            BuildingBuilder.Build(spec, tileset, transform.position, transform);
        }

        // Columns to the right of this spot that are clear of other buildings' blocks.
        private int FreeWidth(int limit)
        {
            var size = new Vector2(0.9f, ScanHeight);
            for (int x = 0; x < limit; x++)
            {
                Vector2 centre = (Vector2)transform.position + new Vector2(x + 0.5f, ScanHeight * 0.5f + 0.1f);
                foreach (Collider2D hit in Physics2D.OverlapBoxAll(centre, size, 0f))
                    if (hit.GetComponentInParent<BuildingBlock>() != null)
                        return Mathf.Max(0, x - 1); // keep a one-cell alley
            }
            return limit;
        }
    }
}
