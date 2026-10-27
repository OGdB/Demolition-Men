using System;
using System.Collections.Generic;
using Demolition.Meta;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Demolition.Ads.UnityIap
{
    /// <summary>
    /// Google Play Billing through Unity IAP 5 on Android, behind <see cref="Purchases"/>. It
    /// connects at start, fetches REMOVE ADS with its price, and fetches what this Google account
    /// bought before, so a reinstall gets it back without a restore button. A purchase is granted
    /// before it is confirmed with the store: if the confirmation fails, Play delivers it again.
    /// Unity Gaming Services aren't started: IAP only needs them for its own analytics.
    /// </summary>
    public sealed class UnityIapPurchases : IShopService
    {
        private readonly StoreController _store;
        private string _buying;
        private Action<bool> _finished;

        private UnityIapPurchases()
        {
            _store = UnityIAPServices.StoreController();
            _store.OnStoreConnected += OnConnected;
            _store.OnStoreDisconnected += failure => Debug.LogWarning($"[Store] disconnected: {failure.Message}");
            _store.OnProductsFetched += _ => _store.FetchPurchases();
            _store.OnProductsFetchFailed += failure => Debug.LogWarning($"[Store] products: {failure.FailureReason}");
            _store.OnPurchasesFetched += OnPurchasesFetched;
            _store.OnPurchasesFetchFailed += failure => Debug.LogWarning($"[Store] purchases: {failure.Message}");
            _store.OnPurchasePending += OnPending;
            _store.OnPurchaseConfirmed += OnConfirmed;
            _store.OnPurchaseFailed += order => Finish(false, $"failed: {order.FailureReason} {order.Details}");
            _store.OnPurchaseDeferred += _ => Finish(false, "deferred: paid later, granted when Play delivers it");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Application.isEditor || Application.platform != RuntimePlatform.Android)
                return;
            var service = new UnityIapPurchases();
            Purchases.Service = service;
            _ = service._store.Connect(); // OnStoreConnected / OnStoreDisconnected report how it went
        }

        public bool IsReady(string productId)
        {
            Product product = _store.GetProductById(productId);
            return product != null && product.availableToPurchase && _buying == null;
        }

        public string Price(string productId) => _store.GetProductById(productId)?.metadata.localizedPriceString ?? string.Empty;

        public void Buy(string productId, Action<bool> finished)
        {
            Product product = _store.GetProductById(productId);
            if (product == null || _buying != null)
            {
                finished?.Invoke(false);
                return;
            }
            _buying = productId;
            _finished = finished;
            _store.PurchaseProduct(product);
        }

        private void OnConnected() =>
            _store.FetchProducts(new List<ProductDefinition> { new ProductDefinition(Purchases.RemoveAds, ProductType.NonConsumable) });

        // Bought before (another install, another device on the same account).
        private static void OnPurchasesFetched(Orders orders)
        {
            foreach (ConfirmedOrder order in orders.ConfirmedOrders)
                GrantAll(order);
        }

        private void OnPending(PendingOrder order)
        {
            GrantAll(order);
            _store.ConfirmPurchase(order);
            Finish(true, null);
        }

        private static void OnConfirmed(Order order)
        {
            if (order is FailedOrder failed)
                Debug.LogWarning($"[Store] confirm failed: {failed.FailureReason} {failed.Details}");
        }

        private static void GrantAll(Order order)
        {
            foreach (CartItem item in order.CartOrdered.Items())
                Purchases.Grant(item.Product.definition.id);
        }

        private void Finish(bool bought, string problem)
        {
            if (problem != null)
                Debug.LogWarning($"[Store] {_buying}: {problem}");
            Action<bool> finished = _finished;
            _buying = null;
            _finished = null;
            finished?.Invoke(bought);
        }
    }
}
