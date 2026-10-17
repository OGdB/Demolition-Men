using System;
using Demolition.Run;

namespace Demolition.Levels
{
    /// <summary>
    /// Which level the run scene builds next: a campaign entry or the daily challenge.
    /// Survives the scene load; set by the level select, read by <see cref="LevelBootstrap"/>.
    /// </summary>
    public static class LevelSelection
    {
        /// <summary>Campaign index of the level being played, -1 for the daily.</summary>
        public static int CampaignIndex { get; private set; } = -1;
        public static LevelSpec Spec { get; private set; }
        public static LevelTheme Theme { get; private set; }

        /// <summary>The menu should reopen the level select (coming back from a run).</summary>
        public static bool ReturnToLevelSelect { get; set; }

        public static bool IsDaily => Spec != null && CampaignIndex < 0;

        public static void PlayCampaign(int index)
        {
            Select(index);
            Load(GameConfig.Instance);
        }

        /// <summary>Choose a campaign level without loading it (tests, tools).</summary>
        public static void Select(int index)
        {
            CampaignLevel level = GameConfig.Instance.Campaign[index];
            CampaignIndex = index;
            Spec = level.Spec;
            Theme = level.Theme;
        }

        public static void PlayDaily(DateTime date)
        {
            GameConfig config = GameConfig.Instance;
            CampaignLevel template = DailyTemplate(config, date);
            CampaignIndex = -1;
            Spec = template.Spec.WithSeed(Daily.Seed(date), Daily.Id(date));
            Spec.name = "DAILY " + date.ToString("yyyy-MM-dd");
            Theme = template.Theme;
            Load(config);
        }

        /// <summary>The campaign level after the current one, or null at the end (or in the daily).</summary>
        public static Action NextAction()
        {
            int next = CampaignIndex + 1;
            if (CampaignIndex < 0 || next >= GameConfig.Instance.Campaign.Count)
                return null;
            return () => PlayCampaign(next);
        }

        // The daily reuses a campaign level's pacing with a fresh seed, rotating by date.
        private static CampaignLevel DailyTemplate(GameConfig config, DateTime date) =>
            config.Campaign[Daily.TemplateIndex(date, config.Campaign.Count)];

        private static void Load(GameConfig config)
        {
            ReturnToLevelSelect = true;
            RunController.LoadScene(config.runScene);
        }

        /// <summary>Forget the selection: the run scene then starts the first unfinished level.</summary>
        public static void Clear()
        {
            Spec = null;
            Theme = null;
            CampaignIndex = -1;
        }

        /// <summary>Nothing selected (started in the editor, or Next from the training yard):
        /// play the first campaign level not finished yet.</summary>
        public static void EnsureSelected()
        {
#if UNITY_EDITOR
            if (EditorRequest())
                return;
#endif
            if (Spec != null)
                return;
            var campaign = GameConfig.Instance.Campaign;
            int index = 0;
            while (index < campaign.Count - 1 && Meta.Profile.Data.IsFinished(campaign[index].Spec.id))
                index++;
            CampaignIndex = index;
            Spec = campaign[index].Spec;
            Theme = campaign[index].Theme;
        }

        /// <summary>Key the Level Lab writes before entering play mode (statics don't survive
        /// the domain reload): "levelId|seed|bot|utcTicks". Read only in the editor.</summary>
        public const string EditorRequestKey = "Demolition.LevelLab.Request";

#if UNITY_EDITOR
        private static bool EditorRequest()
        {
            string request = UnityEngine.PlayerPrefs.GetString(EditorRequestKey, string.Empty);
            if (string.IsNullOrEmpty(request))
                return false;
            UnityEngine.PlayerPrefs.DeleteKey(EditorRequestKey);
            string[] parts = request.Split('|');
            // Written right before entering play mode; an old one is from a start that failed.
            if (parts.Length < 4 || !long.TryParse(parts[3], out long ticks)
                || (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalSeconds > 60)
                return false;
            foreach (CampaignLevel level in GameConfig.Instance.Campaign)
            {
                if (level.Spec.id != parts[0])
                    continue;
                CampaignIndex = level.Index;
                Theme = level.Theme;
                Spec = parts.Length > 1 && int.TryParse(parts[1], out int seed) && seed != level.Spec.seed
                    ? level.Spec.WithSeed(seed, level.Spec.id)
                    : level.Spec;
                LevelBootstrap.AutoPilot = parts.Length > 2 && parts[2] == "bot";
                return true;
            }
            return false;
        }
#endif
    }
}
