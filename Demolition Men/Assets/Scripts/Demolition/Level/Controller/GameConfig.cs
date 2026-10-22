using System.Collections.Generic;
using Demolition.Generation;
using UnityEngine;

namespace Demolition.Levels
{
    /// <summary>One entry of the campaign: a level spec and the theme it is played in.</summary>
    public sealed class CampaignLevel
    {
        public int Index;
        public LevelTheme Theme;
        public LevelSpec Spec;
    }

    /// <summary>
    /// What a runtime-built level is assembled from (players, camera, run, pickups) and
    /// the campaign's themes in order. Lives at <c>Resources/GameConfig</c> so menus and
    /// the run scene find it without scene wiring.
    /// </summary>
    [CreateAssetMenu(menuName = "Demolition/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "GameConfig";

        [Header("Scenes")]
        public string menuScene = "MainMenu";
        public string runScene = "Run";
        public string tutorialScene = "Tutorial";

        [Header("Prefabs")]
        [Tooltip("Player_1, Player_2 (kept by name: the run finds players by GameObject name).")]
        public GameObject[] playerPrefabs = new GameObject[0];
        public GameObject cameraPrefab;
        public GameObject backdropPrefab;
        public GameObject runPrefab;
        public Pickup pickupPrefab;
        [Tooltip("The stick of dynamite a gas tank goes off as (its blast and sound; nobody throws it).")]
        public GameObject dynamitePrefab;
        [Tooltip("The crew's caravan, parked just past every generated level's finish line (the Tutorial places its own). None = no caravan.")]
        public Sprite caravan;

        [Header("Camera")]
        [Tooltip("Players can zoom the run camera (mouse wheel, right stick, pinch). Off: the framing alone decides.")]
        public bool playerZoom = true;

        [Header("Store")]
        [Tooltip("The privacy policy page (served by the relay from server/relay/Relay/wwwroot/privacy.html). CREDITS links to it; Play requires a link inside the app.")]
        public string privacyPolicyUrl = "https://relay.oeds.space/privacy";

        [Header("Online")]
        [Tooltip("The co-op relay (server/relay). ws://localhost:5080/ws for one run with dotnet run.")]
        public string relayUrl = "wss://relay.oeds.space/ws";
        [Tooltip("Player_1 and Player_2 with NetworkObject, NetworkTransform and NetPlayer: Tools ▸ Demolition ▸ Online ▸ Build Network Prefabs.")]
        public GameObject[] netPlayerPrefabs = new GameObject[0];

        [Header("Campaign")]
        public LevelTheme[] themes = new LevelTheme[0];

        private static GameConfig _instance;
        private List<CampaignLevel> _campaign;
        private readonly Dictionary<LevelTheme, List<BuildingArchetype>> _archetypes = new Dictionary<LevelTheme, List<BuildingArchetype>>();

        public static GameConfig Instance => _instance != null ? _instance : _instance = Resources.Load<GameConfig>(ResourcePath);

        /// <summary>Every campaign level in play order, themes one after another.</summary>
        public IReadOnlyList<CampaignLevel> Campaign
        {
            get
            {
                if (_campaign != null)
                    return _campaign;
                _campaign = new List<CampaignLevel>();
                foreach (LevelTheme theme in themes)
                {
                    if (theme == null)
                        continue;
                    foreach (TextAsset level in theme.levels)
                        if (level != null)
                            _campaign.Add(new CampaignLevel { Index = _campaign.Count, Theme = theme, Spec = LevelSpec.FromJson(level.text) });
                }
                return _campaign;
            }
        }

        public IReadOnlyList<BuildingArchetype> Archetypes(LevelTheme theme)
        {
            if (_archetypes.TryGetValue(theme, out List<BuildingArchetype> list))
                return list;
            list = new List<BuildingArchetype>();
            foreach (TextAsset json in theme.archetypes)
                if (json != null)
                    list.Add(BuildingArchetype.FromJson(json.text));
            _archetypes[theme] = list;
            return list;
        }

        public LevelTheme Theme(string id)
        {
            foreach (LevelTheme theme in themes)
                if (theme != null && theme.name.StartsWith(id, System.StringComparison.OrdinalIgnoreCase))
                    return theme;
            return themes.Length > 0 ? themes[0] : null;
        }
    }
}
