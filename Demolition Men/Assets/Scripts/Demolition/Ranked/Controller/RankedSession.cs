using System;
using Demolition.Levels;

namespace Demolition.Ranked
{
    /// <summary>
    /// The ranked attempt in play (docs/ranked.md §2): asked of the server from the RANKED screen (or a
    /// ranked run's RETRY: one run per attempt), its level loaded, its log sealed with the attempt's
    /// key and sent when the run ends, its place on the board kept for the results screen. The day's
    /// best run comes along to race as a ghost.
    /// </summary>
    public static class RankedSession
    {
        public enum Phase { Idle, Starting, Playing, Sending, Sent, Failed }

        private static StartResponse _attempt;
        private static RankedMode _mode;
        private static byte[] _sealed;
        private static bool _sent;

        public static Phase Status { get; private set; }
        /// <summary>The server's answer to the last run sent.</summary>
        public static SubmitResponse Result { get; private set; }
        /// <summary>What went wrong (Starting or Sending), for the player.</summary>
        public static string Error { get; private set; } = string.Empty;
        /// <summary>The day's best run to race, once it arrived: who and their log.</summary>
        public static Ghost Ghost { get; private set; }
        public static RunLog GhostLog { get; private set; }

        public static event Action Changed;

        static RankedSession() => RunRecorder.Completed += OnCompleted;

        private static void Set(Phase phase, string error = "")
        {
            Status = phase;
            Error = error ?? string.Empty;
            Changed?.Invoke();
        }

        /// <summary>Ask for an attempt at today's level and play it; <paramref name="failed"/> hears why not.</summary>
        public static void Begin(RankedMode mode, Action<string> failed = null)
        {
            if (Status == Phase.Starting)
                return;
            _mode = mode;
            Set(Phase.Starting);
            bool partner = mode == RankedMode.Coop && NetRole.IsHost && RankedApi.IsPlayerId(NetRole.PartnerId);
            var request = new StartRequest
            {
                player = PlayerIdentity.Id, name = PlayerIdentity.Name, mode = RankedApi.ModeName(mode),
                partner = partner ? NetRole.PartnerId : string.Empty, partnerName = partner ? NetRole.PartnerName : string.Empty,
                rules = RankedRules.Version,
            };
            RankedClient.Start(request, (response, error) =>
            {
                if (response == null || !RankedApi.TryMode(response.mode, out RankedMode given))
                {
                    Fail(error ?? "THE RANKED SERVER SAID NO", failed);
                    return;
                }
                var day = new RankedDay { Day = response.day, Template = response.template ?? string.Empty, Seed = response.seed, Mode = given };
                if (!LevelSelection.SelectRanked(day))
                {
                    Fail("UPDATE THE GAME TO PLAY RANKED", failed); // the server knows a level this build doesn't
                    return;
                }
                _attempt = response;
                _sealed = null;
                _sent = false;
                Result = null;
                Ghost = null;
                GhostLog = null;
                Set(Phase.Playing);
                RankedClient.Ghost(given, (ghost, _) =>
                {
                    if (ghost == null || _attempt != response || string.IsNullOrEmpty(ghost.log))
                        return;
                    try
                    {
                        GhostLog = RunLog.Decode(Convert.FromBase64String(ghost.log));
                        Ghost = ghost;
                    }
                    catch (FormatException)
                    {
                    }
                });
                LevelSelection.PlayRanked(day);
            });
        }

        private static void Fail(string error, Action<string> failed)
        {
            Set(Phase.Failed, error);
            failed?.Invoke(error);
        }

        /// <summary>A ranked run's RETRY: a fresh attempt at the same board. False outside a ranked run.</summary>
        public static bool Again()
        {
            if (!LevelSelection.IsRanked)
                return false;
            Begin(_attempt != null && RankedApi.TryMode(_attempt.mode, out RankedMode mode) ? mode : _mode);
            return true;
        }

        // The run's log, sealed once and sent; RETRY SENDING sends the same bytes again.
        private static void OnCompleted(RunLog log)
        {
            if (_attempt == null || Status != Phase.Playing || log.Day != _attempt.day || log.Seed != _attempt.seed)
                return; // not the attempt's run (a test's, or a retry already asked for another)
            byte[] key;
            try
            {
                key = Convert.FromBase64String(_attempt.key ?? string.Empty);
            }
            catch (FormatException)
            {
                Set(Phase.Failed, "THE RANKED SERVER SAID NO");
                return;
            }
            _sealed = Seal.Close(log.Encode(), key);
            Send();
        }

        /// <summary>Send the finished run (again, after a failed send).</summary>
        public static void Send()
        {
            if (_attempt == null || _sealed == null || _sent || Status == Phase.Sending)
                return;
            Set(Phase.Sending);
            StartResponse attempt = _attempt;
            RankedClient.Submit(new SubmitRequest { attempt = attempt.attempt, log = Convert.ToBase64String(_sealed) }, (result, error) =>
            {
                if (attempt != _attempt)
                    return;
                if (result == null)
                {
                    Set(Phase.Failed, error ?? "THE RANKED SERVER SAID NO");
                    return;
                }
                Result = result;
                _sent = true;
                Set(Phase.Sent);
            });
        }

        /// <summary>The run being sent can be sent again: it was sealed, and the last try failed.</summary>
        public static bool CanResend => _sealed != null && !_sent && Status == Phase.Failed;

        /// <summary>Out of ranked (back to the menu): nothing is waiting any more.</summary>
        public static void Forget()
        {
            _attempt = null;
            _sealed = null;
            Ghost = null;
            GhostLog = null;
            Set(Phase.Idle);
        }

        /// <summary>Report a run on a board as a cheat.</summary>
        public static void Report(int run, Action<string> done) =>
            RankedClient.Report(new ReportRequest { player = PlayerIdentity.Id, run = run }, done);

        /// <summary>For the screens: an attempt's level as its campaign name ("TOWN 3"), or the id.</summary>
        public static string LevelName(string template)
        {
            if (GameConfig.Instance != null)
                foreach (CampaignLevel level in GameConfig.Instance.Campaign)
                    if (level.Spec.id == template)
                        return level.Spec.name;
            return template ?? string.Empty;
        }
    }
}
