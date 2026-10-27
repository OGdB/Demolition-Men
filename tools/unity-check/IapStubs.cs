// Compile-only stand-ins for the Unity IAP 5 members Demolition.Ads.UnityIap uses: the package
// builds from many asmdefs on top of Unity Gaming Services, too much to build here. Signatures
// copied from com.unity.purchasing 5.4.4 (Runtime/Purchasing). Never run.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnityEngine.Purchasing
{
    public enum ProductType { Consumable, NonConsumable, Subscription }
    public enum PurchaseFailureReason { PurchasingUnavailable, ExistingPurchasePending, ProductUnavailable, SignatureInvalid, UserCancelled, PaymentDeclined, DuplicateTransaction, ValidationFailure, Unknown }
    public enum PurchasesFetchFailureReason { Unknown }

    public class ProductDefinition
    {
        public ProductDefinition(string id, ProductType type) => throw new NotImplementedException();
        public string id { get; private set; }
    }
    public class ProductMetadata { public string localizedPriceString { get; internal set; } }
    public class Product
    {
        public ProductDefinition definition => throw new NotImplementedException();
        public ProductMetadata metadata => throw new NotImplementedException();
        public bool availableToPurchase => throw new NotImplementedException();
    }
    public class CartItem { public Product Product { get; } }
    public interface ICart { IReadOnlyList<CartItem> Items(); }
    public abstract class Order { public ICart CartOrdered { get; internal set; } }
    public class PendingOrder : Order { }
    public class ConfirmedOrder : Order { }
    public class DeferredOrder : Order { }
    public class FailedOrder : Order
    {
        public PurchaseFailureReason FailureReason => throw new NotImplementedException();
        public string Details => throw new NotImplementedException();
    }
    public class Orders
    {
        public IReadOnlyList<ConfirmedOrder> ConfirmedOrders { get; }
        public IReadOnlyList<PendingOrder> PendingOrders { get; }
    }
    public class StoreConnectionFailureDescription { public string Message => throw new NotImplementedException(); }
    public class ProductFetchFailed { public string FailureReason { get; } }
    public class PurchasesFetchFailureDescription { public string Message => throw new NotImplementedException(); }
    public interface IRetryPolicy { }

    public class StoreController
    {
        public event Action<StoreConnectionFailureDescription> OnStoreDisconnected;
        public event Action OnStoreConnected;
        public event Action<PendingOrder> OnPurchasePending;
        public event Action<Order> OnPurchaseConfirmed;
        public event Action<FailedOrder> OnPurchaseFailed;
        public event Action<DeferredOrder> OnPurchaseDeferred;
        public event Action<Orders> OnPurchasesFetched;
        public event Action<PurchasesFetchFailureDescription> OnPurchasesFetchFailed;
        public event Action<List<Product>> OnProductsFetched;
        public event Action<ProductFetchFailed> OnProductsFetchFailed;
        public Task Connect() => throw new NotImplementedException();
        public void FetchProducts(List<ProductDefinition> productDefinitions, IRetryPolicy retryPolicy = null) => throw new NotImplementedException();
        public Product GetProductById(string catalogListingId) => throw new NotImplementedException();
        public void PurchaseProduct(Product product) => throw new NotImplementedException();
        public void ConfirmPurchase(PendingOrder order) => throw new NotImplementedException();
        public void FetchPurchases() => throw new NotImplementedException();
    }

    public static class UnityIAPServices
    {
        public static StoreController StoreController(string storeName = null) => throw new NotImplementedException();
    }
}
