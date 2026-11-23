using System;
using Demolition.Levels;
using UnityEngine;

namespace Demolition.Meta
{
    /// <summary>
    /// Sends the C# exceptions Unity logs to our own server (server/relay's /error), as
    /// <see cref="ErrorReports"/> decides, fire and forget like the counters. Off until
    /// <see cref="GameConfig.errorsUrl"/> is set, and always off in the editor and on a temporary profile.
    /// Native crashes and freezes aren't here: Android vitals in the Play Console has those.
    /// </summary>
    public static class ErrorReporter
    {
        private static readonly ErrorReports Reports = new ErrorReports();
        private static bool _busy;

        /// <summary>Listen to the log; again is harmless.</summary>
        public static void Install()
        {
            // shortcut: main-thread exceptions only (logMessageReceived); the game has no threads of its own, use logMessageReceivedThreaded if it gets some.
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception || _busy)
                return;
            _busy = true; // whatever this logs itself is not reported
            try
            {
                string url = GameConfig.Instance != null ? GameConfig.Instance.errorsUrl : null;
                if (!Counters.MaySend(url, Application.isEditor, Profile.IsTemporary))
                    return;
                ErrorReport report = Reports.Take(condition, stackTrace, Application.version, CounterSender.PlatformName,
                    SystemInfo.deviceModel, new[]
                    {
                        Application.persistentDataPath, Application.temporaryCachePath, Application.dataPath,
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    });
                if (report != null)
                    CounterSender.Send(url, JsonUtility.ToJson(report));
            }
            catch (Exception) // a report is never worth another error
            {
            }
            finally
            {
                _busy = false;
            }
        }
    }
}
