using System;
using System.Text;
using Demolition.Levels;
using UnityEngine;
using UnityEngine.Networking;

namespace Demolition.Meta
{
    /// <summary>
    /// Posts what <see cref="Counters"/> counts to our own server (server/relay's /count), fire and
    /// forget: no reply is read and nothing is queued, so a count that doesn't get through (offline)
    /// is lost. Off until <see cref="GameConfig.countersUrl"/> is set, and always off in the editor
    /// and on a temporary profile (<see cref="Counters.MaySend"/>).
    /// </summary>
    public static class CounterSender
    {
        private const int TimeoutSeconds = 10;

        private static string Url => GameConfig.Instance != null ? GameConfig.Instance.countersUrl : null;

        private static bool On => Counters.MaySend(Url, Application.isEditor, Profile.IsTemporary);

        /// <summary>The app started or came back to the front.</summary>
        public static void Session()
        {
            if (On && Counters.Session(Profile.Data.counters, DateTime.Now, PlatformName, Post))
                Profile.Save();
        }

        /// <summary>A run reached the finish; the caller saves the profile.</summary>
        public static void LevelFinished(string levelId, int stars, int players, int online, bool firstTutorial)
        {
            if (On)
                Counters.LevelFinished(Profile.Data.counters, levelId, stars, players, online, firstTutorial, PlatformName, Post);
        }

        public static void Shared()
        {
            if (On)
                Counters.Shared(PlatformName, Post);
        }

        public static void Invited()
        {
            if (On)
                Counters.Invited(PlatformName, Post);
        }

        /// <summary>web, android, pc or other: the platform as the relay's /count and /error name it.</summary>
        internal static string PlatformName
        {
            get
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.WebGLPlayer: return "web";
                    case RuntimePlatform.Android: return "android";
                    case RuntimePlatform.WindowsPlayer:
                    case RuntimePlatform.LinuxPlayer:
                    case RuntimePlatform.OSXPlayer: return "pc";
                    default: return "other";
                }
            }
        }

        private static void Post(CountEvent count) => Send(Url, JsonUtility.ToJson(count));

        /// <summary>Posts <paramref name="json"/> to <paramref name="url"/>, fire and forget (the counters, <see cref="ErrorReporter"/>).</summary>
        internal static void Send(string url, string json)
        {
            try
            {
                var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                    timeout = TimeoutSeconds,
                };
                request.SetRequestHeader("Content-Type", "application/json");
                request.SendWebRequest().completed += _ => request.Dispose();
            }
            catch (Exception) // a bad address: a count or a report is never worth an error in the game
            {
            }
        }
    }
}
