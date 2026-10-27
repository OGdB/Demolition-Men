using System;
using Demolition.Meta;
using UnityEngine;

namespace Demolition.Ads
{
    /// <summary>
    /// A store as the game sees it. Implementations wrap an SDK (Android: the Demolition.Ads.UnityIap
    /// assembly, Google Play through Unity IAP). Callbacks run on the main thread. Every purchase
    /// the store confirms, now or from before (a reinstall), goes to <see cref="Purchases.Grant"/>.
    /// </summary>
    public interface IShopService
    {
        /// <summary>The store knows the product and it can be bought now.</summary>
        bool IsReady(string productId);

        /// <summary>Its price in the player's currency, as the store writes it ("€2,99").</summary>
        string Price(string productId);

        /// <summary>Start a purchase; <paramref name="finished"/> gets true once it went through.</summary>
        void Buy(string productId, Action<bool> finished);
    }

    /// <summary>No store: PC, TV, the browser, and phones without the IAP package.</summary>
    public sealed class NoShopService : IShopService
    {
        public bool IsReady(string productId) => false;
        public string Price(string productId) => string.Empty;
        public void Buy(string productId, Action<bool> finished) => finished?.Invoke(false);
    }

    /// <summary>The editor with <see cref="AdManager.SimulateInEditor"/>: every purchase goes through.</summary>
    public sealed class SimulatedShopService : IShopService
    {
        public bool IsReady(string productId) => true;
        public string Price(string productId) => "€0.00";

        public void Buy(string productId, Action<bool> finished)
        {
            Debug.Log($"[Store] simulated purchase of {productId}");
            Purchases.Grant(productId);
            finished?.Invoke(true);
        }
    }

    /// <summary>
    /// The game's one door to in-app purchases. There is one product, REMOVE ADS, offered only
    /// where ads show and a store sells it: phones with Google Play. A browser, PC or TV build shows
    /// no ads and has no store, so it never offers it.
    /// </summary>
    public static class Purchases
    {
        /// <summary>The Play Console product id (a one-time product); permanent once created.</summary>
        public const string RemoveAds = "remove_ads";

        private static IShopService _service;

        public static IShopService Service
        {
            get => _service ??= Application.isEditor && AdManager.SimulateInEditor ? new SimulatedShopService() : new NoShopService();
            set => _service = value;
        }

        /// <summary>Something was bought or restored: menus showing the offer refresh.</summary>
        public static event Action Granted;

        /// <summary>REMOVE ADS can be offered right now.</summary>
        public static bool CanBuyRemoveAds => AdManager.ShowsAds && Service.IsReady(RemoveAds);

        public static string RemoveAdsPrice => Service.Price(RemoveAds);

        public static void BuyRemoveAds(Action<bool> finished)
        {
            if (!CanBuyRemoveAds)
            {
                finished?.Invoke(false);
                return;
            }
            Service.Buy(RemoveAds, finished);
        }

        /// <summary>The store confirmed <paramref name="productId"/> (bought now, or before on this account).</summary>
        public static void Grant(string productId)
        {
            if (productId != RemoveAds || Profile.Data.adsRemoved)
                return;
            Profile.Data.adsRemoved = true;
            Profile.Save();
            Granted?.Invoke();
        }
    }
}
