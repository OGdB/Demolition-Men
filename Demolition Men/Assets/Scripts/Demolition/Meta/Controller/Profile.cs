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

        public static SaveData Data => _data ??= Load();

        /// <summary>Raised after settings changed, so live systems pick them up.</summary>
        public static event Action SettingsChanged;

        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static void Save()
        {
            if (_data == null)
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
