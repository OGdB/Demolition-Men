using System;
using System.Collections.Generic;
using Demolition.Run;

namespace Demolition.Meta
{
    /// <summary>Player preferences, saved with the progress.</summary>
    [Serializable]
    public sealed class GameSettings
    {
        public float masterVolume = 1f;
        public float musicVolume = 0.6f;
        public bool screenShake = true;
        /// <summary>2D lights and post-processing (bloom, vignette); off for slow phones.</summary>
        public bool effects = true;
        /// <summary>A <see cref="QualityChoice"/>; AUTO lets the device decide (<see cref="QualityPolicy"/>).</summary>
        public int quality;
        /// <summary>A <see cref="TouchControlsMode"/>.</summary>
        public int touchControls;
        public float touchScale = 1f;
        public float touchOpacity = 0.55f;
        /// <summary>In-run HUD size; 0 = automatic (bigger on phones).</summary>
        public float hudScale;

        /// <summary>The HUD canvas stays at least this many reference units wide, so the
        /// player panels and the meter never collide.</summary>
        public const float MinHudWidth = 1750f;

        /// <summary>Largest HUD scale a screen of this aspect fits. Canvases expand to keep
        /// the whole 1920x1080 reference on screen, so a wider screen adds width.</summary>
        public static float HudScaleCap(float aspect) => System.Math.Max(1920f, 1080f * aspect) / MinHudWidth;

        /// <summary>The HUD size to use on this device (the setting, or automatic), within the cap.</summary>
        public float HudScale(bool mobile, float aspect) =>
            System.Math.Min(hudScale > 0f ? hudScale : mobile ? 1.3f : 1f, HudScaleCap(aspect));
    }

    /// <summary>Best result on one level.</summary>
    [Serializable]
    public sealed class LevelRecord
    {
        public string id;
        public bool finished;
        /// <summary>A <see cref="Grade"/>, -1 before the first finish.</summary>
        public int bestGrade = -1;
        /// <summary>The best <see cref="Grade"/> finished within par (its stars show rainbow), -1 for none.</summary>
        public int bestParGrade = -1;
        public int bestScore;
        public float bestTime;
        /// <summary>The job sheet's objectives done (<see cref="Objectives"/>): bit i for objective i.</summary>
        public int objectives;
    }

    [Serializable]
    public sealed class UpgradeRecord
    {
        public string id;
        public int level;
    }

    /// <summary>What a finished or failed run changed in the profile.</summary>
    public struct RunReward
    {
        public int Cash;
        public bool FirstClear;
        public bool NewBestScore;
        public bool NewBestGrade;
        /// <summary>More rainbow stars (a better grade within par) than before.</summary>
        public bool NewRainbow;
        /// <summary>The job sheet's objectives this run met (bit i for objective i), and those
        /// done for the first time, which paid <see cref="ObjectiveCash"/>.</summary>
        public int ObjectivesMet;
        public int NewObjectives;
        public int ObjectiveCash;
        /// <summary>The Tutorial's first finish paid the crew's <see cref="Economy.StartingCash"/>.</summary>
        public int StartingCash;
    }

    /// <summary>
    /// Everything the game remembers between sessions: progress per level, banked cash,
    /// upgrades and settings. Plain fields so <c>JsonUtility</c> can write it.
    /// </summary>
    /// <summary>One player's look items, by id.</summary>
    [Serializable]
    public sealed class PlayerLooks
    {
        public List<string> items = new List<string>();
    }

    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int cash;
        public int lifetimeCash;
        public int runsPlayed;
        public int runsFinished;
        public List<LevelRecord> levels = new List<LevelRecord>();
        public List<UpgradeRecord> upgrades = new List<UpgradeRecord>();
        public GameSettings settings = new GameSettings();
        /// <summary>Runs ended since the last interstitial ad.</summary>
        public int runsSinceAd;
        /// <summary>Bought REMOVE ADS (the store's <c>remove_ads</c>); it also brings the supporter's look.</summary>
        public bool adsRemoved;
        /// <summary>The action each player carries into a level, by id (<c>ActionCatalog</c>); P1 first.</summary>
        public List<string> actions = new List<string>();
        /// <summary>Actions bought in the shop, by id; the free one isn't listed.</summary>
        public List<string> ownedActions = new List<string>();
        /// <summary>Look items bought in the shop, by id (<c>LookCatalog</c>).</summary>
        public List<string> looks = new List<string>();
        /// <summary>The look items each player wears, P1 first: one per slot at most.</summary>
        public List<PlayerLooks> dressed = new List<PlayerLooks>();
        /// <summary>The name on the ranked boards (Ranked.PlayerName), typed on the first screen; empty: the generated one.</summary>
        public string playerName = string.Empty;
        /// <summary>The action each player carries into a ranked run, by id, P1 first: any action, owned or
        /// not (docs/ranked.md K-D2), picked apart from <see cref="actions"/> so the campaign's stays its own.</summary>
        public List<string> rankedActions = new List<string>();

        public LevelRecord Level(string id)
        {
            foreach (LevelRecord record in levels)
                if (record.id == id)
                    return record;
            return null;
        }

        public bool IsFinished(string id) => Level(id)?.finished ?? false;

        /// <summary>Levels (and dailies) with every objective on their job sheet done.</summary>
        public int FullSheets()
        {
            int count = 0;
            foreach (LevelRecord record in levels)
                if ((record.objectives & Objectives.AllDone) == Objectives.AllDone)
                    count++;
            return count;
        }

        public int UpgradeLevel(string id)
        {
            foreach (UpgradeRecord record in upgrades)
                if (record.id == id)
                    return record.level;
            return 0;
        }

        public void SetUpgradeLevel(string id, int level)
        {
            foreach (UpgradeRecord record in upgrades)
            {
                if (record.id != id)
                    continue;
                record.level = level;
                return;
            }
            upgrades.Add(new UpgradeRecord { id = id, level = level });
        }

        public void AddCash(int amount)
        {
            if (amount <= 0)
                return;
            cash += amount;
            lifetimeCash += amount;
        }

        /// <summary>Bank a run: cash from its score, best results, run counters, and the job
        /// sheet's objectives it met (<paramref name="objectivesMet"/>, bit i for objective i;
        /// each pays once). The <paramref name="tutorial"/>'s first finish pays the starting cash.</summary>
        public RunReward RecordRun(string levelId, bool finished, Grade grade, int score, float time, bool withinPar = false,
            int objectivesMet = 0, bool tutorial = false)
        {
            runsPlayed++;
            runsSinceAd++;
            var reward = new RunReward { Cash = Economy.CashFor(score, finished) };
            AddCash(reward.Cash);
            if (string.IsNullOrEmpty(levelId))
                return reward;

            LevelRecord record = Level(levelId);
            if (record == null)
            {
                record = new LevelRecord { id = levelId };
                levels.Add(record);
            }
            if (score > record.bestScore)
            {
                reward.NewBestScore = record.bestScore > 0 || finished;
                record.bestScore = score;
            }
            if (!finished)
                return reward;

            runsFinished++;
            reward.FirstClear = !record.finished;
            if (!record.finished || time < record.bestTime)
                record.bestTime = time;
            record.finished = true;
            if ((int)grade > record.bestGrade)
            {
                reward.NewBestGrade = record.bestGrade >= 0;
                record.bestGrade = (int)grade;
            }
            if (withinPar && (int)grade > record.bestParGrade)
            {
                reward.NewRainbow = grade > Grade.C; // a C has no stars to colour
                record.bestParGrade = (int)grade;
            }
            reward.ObjectivesMet = objectivesMet & Objectives.AllDone;
            reward.NewObjectives = reward.ObjectivesMet & ~record.objectives;
            reward.ObjectiveCash = Objectives.Count(reward.NewObjectives) * Objectives.CashEach;
            AddCash(reward.ObjectiveCash);
            record.objectives |= reward.ObjectivesMet;
            if (tutorial && reward.FirstClear)
            {
                reward.StartingCash = Economy.StartingCash;
                AddCash(reward.StartingCash);
            }
            return reward;
        }
    }
}
