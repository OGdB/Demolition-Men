using System;
using System.Text;
using Demolition.Meta;
using UnityEngine;

namespace Demolition.Ranked
{
    /// <summary>
    /// Who this device is to the ranked server (docs/ranked.md §8): one player, one name and one
    /// board entry per device. The id is a hash of the device's own identifier, so the server never
    /// sees that; where a platform has none (WebGL), a random one kept in this browser's storage.
    /// </summary>
    public static class PlayerIdentity
    {
        private const string FallbackKey = "Demolition.PlayerDevice";
        private static string _id;

        /// <summary>32 hex characters, the same on every run of this device.</summary>
        public static string Id => _id ??= Compute();

        /// <summary>The name on the boards: the one typed, else the generated one.</summary>
        public static string Name
        {
            get
            {
                string typed = Profile.Data.playerName;
                return PlayerName.Allowed(typed) ? typed : PlayerName.Default(Id);
            }
        }

        /// <summary>Keep <paramref name="typed"/> as the name, cleaned. False (and nothing kept) when it isn't allowed.</summary>
        public static bool Rename(string typed)
        {
            string name = PlayerName.Clean(typed);
            if (!PlayerName.Allowed(name))
                return false;
            Profile.Data.playerName = name;
            Profile.Save();
            return true;
        }

        private static string Compute()
        {
            string device = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrEmpty(device) || device == SystemInfo.unsupportedIdentifier)
            {
                device = PlayerPrefs.GetString(FallbackKey, string.Empty);
                if (string.IsNullOrEmpty(device))
                {
                    device = Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString(FallbackKey, device);
                    PlayerPrefs.Save();
                }
            }
            byte[] hash = Seal.Sha256(Encoding.UTF8.GetBytes("demolition-men/player/" + device));
            var hex = new StringBuilder(32);
            for (int i = 0; i < 16; i++)
                hex.Append(hash[i].ToString("x2"));
            return hex.ToString();
        }
    }
}
