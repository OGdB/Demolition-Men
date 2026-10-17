using System;
using System.IO;
using UnityEngine;

namespace Demolition.Meta
{
    /// <summary>
    /// The saved profile: loaded on first use from <c>persistentDataPath/profile.json</c>,
    /// written whole on <see cref="Save"/> (to a temp file first, so a crash mid-write
    /// leaves one good copy). A corrupt file is kept as <c>.bad</c> and a fresh profile starts.
    /// </summary>
    public static class Profile
    {
        private const string FileName = "profile.json";
        private static SaveData _data;
        private static bool _temporary;

        public static SaveData Data => _data ??= Load();

        /// <summary>Raised after settings changed, so live systems pick them up.</summary>
        public static event Action SettingsChanged;

        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Tests and tools: play on a fresh in-memory profile; nothing touches the
        /// player's save until <see cref="UseDisk"/>.</summary>
        public static void UseTemporary()
        {
            _data = new SaveData();
            _temporary = true;
        }

        /// <summary>Back to the saved profile (reloaded on next use).</summary>
        public static void UseDisk()
        {
            _data = null;
            _temporary = false;
        }

        public static void Save()
        {
            if (_data == null || _temporary)
                return;
            string path = FilePath;
            string temp = path + ".tmp";
            try
            {
                File.WriteAllText(temp, JsonUtility.ToJson(_data, true));
                File.Copy(temp, path, true);
                File.Delete(temp);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Profile not saved: {e.Message}");
            }
        }

        public static void NotifySettingsChanged()
        {
            Save();
            SettingsChanged?.Invoke();
        }

        /// <summary>Start over (settings are kept).</summary>
        public static void ResetProgress()
        {
            GameSettings settings = Data.settings;
            _data = new SaveData { settings = settings };
            Save();
        }

        private static SaveData Load()
        {
            string path = FilePath;
            return Read(path) ?? Read(path + ".tmp") ?? new SaveData();
        }

        private static SaveData Read(string path)
        {
            if (!File.Exists(path))
                return null;
            try
            {
                SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (data == null)
                    throw new InvalidDataException("empty profile");
                data.settings ??= new GameSettings();
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Profile unreadable, starting fresh: {e.Message}");
                try { File.Copy(path, path + ".bad", true); } catch (IOException) { }
                return null;
            }
        }
    }
}
