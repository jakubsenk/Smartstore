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
using Smartstore.PayU.Configuration;
using Smartstore.PayU.Controllers;
using Smartstore.PayU.Models;
using Smartstore.PayU.Services;

namespace Smartstore.PayU.Providers
{
    [SystemName("Payments.PayU")]
    [FriendlyName("PayU")]
    [Order(1)]
    public class PayUProvider : PaymentMethodBase, IConfigurable
    {
        public override PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;
        public override bool RequiresInteraction => true;
        public override bool SupportVoid => true;
        public override bool SupportRefund => true;
        public override bool SupportPartiallyRefund => true;
        public override bool RequiresPaymentSelection => false;

        private readonly ICheckoutStateAccessor _checkoutStateAccessor;
        private readonly IStoreContext _storeContext;
        private readonly ISettingFactory _settingFactory;
        private readonly ICommonServices _services;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IOrderCalculationService _orderCalculationService;
        private readonly ICurrencyService _currencyService;
        private readonly IRoundingHelper _roundingHelper;
        private readonly PayUGateService _payService;
        public static string SystemName => "Payments.PayU";

        private PayUPaymentResult payResult;

        public PayUProvider(
                    ICommonServices services,
                    IStoreContext storeContext,
                    IShoppingCartService shoppingCartService,
                    IOrderCalculationService orderCalculationService,
                    ICheckoutStateAccessor checkoutStateAccessor,
                    ISettingFactory settingFactory,
                    ICurrencyService currencyService,
                    IRoundingHelper roundingHelper,
                    PayUGateService payService
                    )
        {
            _services = services;
            _storeContext = storeContext;
            _shoppingCartService = shoppingCartService;
            _orderCalculationService = orderCalculationService;
            _checkoutStateAccessor = checkoutStateAccessor;
            _settingFactory = settingFactory;
            _payService = payService;
            _currencyService = currencyService;
            _roundingHelper = roundingHelper;
        }

        public RouteInfo GetConfigurationRoute()
            => new(nameof(PayUAdminController.Configure), "PayUAdmin", new { area = "Admin" });

        public override Widget GetPaymentInfoWidget() => null;

        public override async Task<(decimal FixedFeeOrPercentage, bool UsePercentage)> GetPaymentFeeInfoAsync(ShoppingCart cart)
        {
            PayUSettings settings = await _settingFactory.LoadSettingsAsync<PayUSettings>(_storeContext.CurrentStore.Id);

            return (settings.AdditionalFee, settings.AdditionalFeePercentage);
        }


        public override async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            if (processPaymentRequest.OrderGuid == Guid.Empty)
            {
                throw new Exception($"{nameof(processPaymentRequest.OrderGuid)} is missing.");
            }

            ProcessPaymentResult result = new ProcessPaymentResult();


            PayUPaymentRequest request = await _payService.GetPayUPaymentRequestAsync(processPaymentRequest);

            payResult = await _payService.InitializePayment(request);

            result.NewPaymentStatus = PaymentStatus.Authorized;
            result.AuthorizationTransactionCode = payResult.TransactionID;
            result.AuthorizationTransactionId = payResult.TransactionID;

            return result;
        }

        public override Task<PostProcessPaymentRequest> PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            postProcessPaymentRequest.RedirectUrl = payResult.RedirectUri;
            postProcessPaymentRequest.IsRePostProcessPayment = false;
            return Task.FromResult(postProcessPaymentRequest);
        }

        public override async Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        {
            Order order = voidPaymentRequest.Order;
            VoidPaymentResult result = new VoidPaymentResult
            {
                NewPaymentStatus = order.PaymentStatus
            };

            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                return result;
            }

            if (await _payService.VoidPayment(order.AuthorizationTransactionId))
            {
                result.NewPaymentStatus = PaymentStatus.Voided;
            }
            return result;
        }

        public override async Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        {
            Order order = refundPaymentRequest.Order;
            RefundPaymentResult result = new RefundPaymentResult
            {
                NewPaymentStatus = order.PaymentStatus,
            };

            if (order.PaymentStatus != PaymentStatus.Paid)
            {
                return result;
            }

            PayURefundResult refundResult;
            if (refundPaymentRequest.IsPartialRefund)
            {
                refundResult = await _payService.RefundPayment(order.AuthorizationTransactionId, refundPaymentRequest.AmountToRefund);
            }
            else
            {
                refundResult = await _payService.RefundPayment(order.AuthorizationTransactionId, null);
            }
            if (refundResult.Success)
            {
                result.NewPaymentStatus = refundPaymentRequest.IsPartialRefund ? PaymentStatus.PartiallyRefunded : PaymentStatus.Refunded;
            }
            return result;
        }
    }
}
