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
    }

    /// <summary>
    /// Everything the game remembers between sessions: progress per level, banked cash,
    /// upgrades and settings. Plain fields so <c>JsonUtility</c> can write it.
    /// </summary>
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
        /// <summary>Bought "remove ads" (or a platform without ads).</summary>
        public bool adsRemoved;

        public LevelRecord Level(string id)
        {
            foreach (LevelRecord record in levels)
                if (record.id == id)
                    return record;
            return null;
        }

        public bool IsFinished(string id) => Level(id)?.finished ?? false;

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

        /// <summary>Bank a run: cash from its score, best results, run counters.</summary>
        public RunReward RecordRun(string levelId, bool finished, Grade grade, int score, float time, bool withinPar = false)
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
            return reward;
        }
    }
}
