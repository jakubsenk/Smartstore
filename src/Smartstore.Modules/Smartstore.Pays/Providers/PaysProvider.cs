using Smartstore.Core;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Common.Services;
using Smartstore.Core.Configuration;
using Smartstore.Core.Stores;
using Smartstore.Core.Widgets;
using Smartstore.Engine.Modularity;
using Smartstore.Http;
using Smartstore.Pays.Configuration;
using Smartstore.Pays.Controllers;
using Currency = Smartstore.Core.Common.Currency;

namespace Smartstore.Pays.Providers
{
    [SystemName("Payments.Pays")]
    [FriendlyName("Pays")]
    [Order(1)]
    public class PaysProvider : PaymentMethodBase, IConfigurable
    {
        public override PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;
        public override bool RequiresInteraction => true;
        public override bool SupportVoid => false;
        public override bool SupportRefund => false;
        public override bool SupportPartiallyRefund => false;
        public override bool RequiresPaymentSelection => false;

        private readonly IStoreContext _storeContext;
        private readonly ISettingFactory _settingFactory;
        private readonly ICommonServices _services;
        private readonly IRoundingHelper _roundingHelper;
        private readonly PaysSettings _paysSettings;
        public static string SystemName => "Payments.Pays";

        private decimal payAmount;
        private string payCurrency;

        public PaysProvider(
                    ICommonServices services,
                    IStoreContext storeContext,
                    ISettingFactory settingFactory,
                    IRoundingHelper roundingHelper,
                    PaysSettings paysSettings
                    )
        {
            _services = services;
            _storeContext = storeContext;
            _settingFactory = settingFactory;
            _roundingHelper = roundingHelper;
            _paysSettings = paysSettings;
        }

        public RouteInfo GetConfigurationRoute()
            => new(nameof(PaysAdminController.Configure), "PaysAdmin", new { area = "Admin" });

        public override Widget GetPaymentInfoWidget() => null;

        public override async Task<(decimal FixedFeeOrPercentage, bool UsePercentage)> GetPaymentFeeInfoAsync(ShoppingCart cart)
        {
            PaysSettings settings = await _settingFactory.LoadSettingsAsync<PaysSettings>(_storeContext.CurrentStore.Id);

            return (settings.AdditionalFee, settings.AdditionalFeePercentage);
        }


        public override async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            if (processPaymentRequest.OrderGuid == Guid.Empty)
            {
                throw new Exception($"{nameof(processPaymentRequest.OrderGuid)} is missing.");
            }
            ProcessPaymentResult result = new ProcessPaymentResult();

            Currency currency = _services.WorkContext.WorkingCurrency;
            payAmount = _roundingHelper.ToSmallestCurrencyUnit(processPaymentRequest.OrderTotal, currency);
            payCurrency = currency.CurrencyCode;

            result.NewPaymentStatus = PaymentStatus.Authorized;

            return result;
        }

        public override Task<PostProcessPaymentRequest> PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            postProcessPaymentRequest.RedirectUrl =
                $"https://www.pays.cz/paymentorder?Merchant={_paysSettings.Merchant}&" +
                $"Shop={_paysSettings.Shop}&" +
                $"Amount={payAmount}&Currency={payCurrency}&" +
                $"MerchantOrderNumber={postProcessPaymentRequest.Order.Id}&" +
                $"Email={postProcessPaymentRequest.Order.Customer.BillingAddress.Email}";
            postProcessPaymentRequest.IsRePostProcessPayment = false;
            return Task.FromResult(postProcessPaymentRequest);
        }
    }
}
