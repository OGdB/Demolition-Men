using System;
using System.Text;
using Demolition.Levels;
using UnityEngine;
using UnityEngine.Networking;

namespace Demolition.Ranked
{
    /// <summary>
    /// The ranked server's API (server/ranked, <see cref="RankedApi"/>) over HTTPS, WebGL included. Each
    /// call answers once on the main thread: the reply, or null and what to tell the player.
    /// </summary>
    public static class RankedClient
    {
        private const int TimeoutSeconds = 15;

        private static string Url => GameConfig.Instance != null ? GameConfig.Instance.rankedUrl.TrimEnd('/') : string.Empty;

        public static void Start(StartRequest request, Action<StartResponse, string> done) => Post("/start", request, done);

        public static void Submit(SubmitRequest request, Action<SubmitResponse, string> done) => Post("/submit", request, done);

        public static void Report(ReportRequest request, Action<string> done) =>
            Send(UnityWebRequest.kHttpVerbPOST, "/report", JsonUtility.ToJson(request), (text, error) => done?.Invoke(error));

        public static void Board(RankedMode mode, string player, Action<Board, string> done) =>
            Get($"/board?mode={RankedApi.ModeName(mode)}&player={player}", done);

        /// <summary>The day's best run to race, or null with no error when there is none yet.</summary>
        public static void Ghost(RankedMode mode, Action<Ghost, string> done) => Get($"/ghost?mode={RankedApi.ModeName(mode)}", done);

        private static void Post<T>(string path, object body, Action<T, string> done) where T : class =>
            Send(UnityWebRequest.kHttpVerbPOST, path, JsonUtility.ToJson(body), (text, error) => done?.Invoke(Parse<T>(text), error));

        private static void Get<T>(string path, Action<T, string> done) where T : class =>
            Send(UnityWebRequest.kHttpVerbGET, path, null, (text, error) => done?.Invoke(Parse<T>(text), error));

        private static T Parse<T>(string text) where T : class
        {
            if (string.IsNullOrEmpty(text))
                return null;
            try
            {
                return JsonUtility.FromJson<T>(text);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static void Send(string method, string path, string json, Action<string, string> done)
        {
            if (string.IsNullOrEmpty(Url))
            {
                done(null, "NO RANKED SERVER IN THIS BUILD");
                return;
            }
            var request = new UnityWebRequest(Url + path, method) { downloadHandler = new DownloadHandlerBuffer(), timeout = TimeoutSeconds };
            if (json != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.SendWebRequest().completed += _ =>
            {
                long status = request.responseCode;
                string text = request.downloadHandler.text;
                bool ok = request.result == UnityWebRequest.Result.Success;
                request.Dispose();
                done(ok && status != 204 ? text : null, ok ? null : Message(status));
            };
        }

        private static string Message(long status)
        {
            switch (status)
            {
                case 0: return "OFFLINE: CHECK THE CONNECTION";
                case 426: return "UPDATE THE GAME TO PLAY RANKED";
                case 429: return "TOO MANY TRIES: TRY AGAIN LATER";
                case 410: return "TOO LATE TO SEND THIS RUN";
                default: return $"THE RANKED SERVER SAID NO ({status})";
            }
        }
    }
}
