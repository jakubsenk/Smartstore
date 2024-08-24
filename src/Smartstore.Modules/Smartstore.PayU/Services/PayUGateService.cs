using System.Net.Http.Headers;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Net.Http.Json;
using System.Net.Http;
using Smartstore.PayU.Configuration;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Common.Services;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core;
using Smartstore.PayU.Models;
using Smartstore.Core.Localization;
using Smartstore.Core.Catalog.Pricing;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Checkout.Tax;
using Smartstore.Core.Checkout.Payment;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Smartstore.Core.Common;

namespace Smartstore.PayU.Services
{
    public class PayUGateService
    {
        private string endpoint;
        private bool sandboxCurrency;
        private string sandboxWebhook;
        private string posid;
        private string description;

        private readonly ICommonServices _services;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IOrderCalculationService _orderCalculationService;
        private readonly ICurrencyService _currencyService;
        private readonly IRoundingHelper _roundingHelper;
        private readonly IProductService _productService;
        private readonly ITaxService _taxService;
        private readonly IPriceCalculationService _priceCalculationService;
        private readonly PayUGateAuthorizationService _authorizationService;

        public ILogger Logger { get; set; } = NullLogger.Instance;


        public PayUGateService(
            ICommonServices services,
            IShoppingCartService shoppingCartService,
            IOrderCalculationService orderCalculationService,
            ICurrencyService currencyService,
            IRoundingHelper roundingHelper,
            ITaxService taxService,
            IPriceCalculationService priceCalculationService,
            IProductService productService,
            PayUSettings config,
            PayUGateAuthorizationService authorizationService
            )
        {
            _services = services;
            _shoppingCartService = shoppingCartService;
            _orderCalculationService = orderCalculationService;
            _currencyService = currencyService;
            _roundingHelper = roundingHelper;
            _taxService = taxService;
            _priceCalculationService = priceCalculationService;
            _productService = productService;
            _authorizationService = authorizationService;

            endpoint = config.IsSandbox ? config.SandboxEndpoint : config.Endpoint;
            posid = config.IsSandbox ? config.SandboxPosID : config.PosID;
            sandboxCurrency = config.IsSandbox;
            sandboxWebhook = config.IsSandbox ? config.SandboxWebhook : null;
            description = config.Description;
        }

        public async Task<PayUPaymentRequest> GetPayUPaymentRequestAsync(ProcessPaymentRequest processPaymentRequest)
        {
            var store = _services.StoreContext.CurrentStore;
            var customer = _services.WorkContext.CurrentCustomer;
            var currency = _services.WorkContext.WorkingCurrency;
            var cart = await _shoppingCartService.GetCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
            // Get subtotal
            var cartSubTotal = await _orderCalculationService.GetShoppingCartSubtotalAsync(cart, true);
            var subTotalConverted = _currencyService.ConvertFromPrimaryCurrency(cartSubTotal.SubtotalWithDiscount.Amount, currency);

            var cartProducts = cart.Items.Select(x => x.Item.Product).ToArray();
            var batchContext = _productService.CreateProductBatchContext(cartProducts, null, customer, false);
            var calculationOptions = _priceCalculationService.CreateDefaultOptions(false, customer, currency, batchContext);

            var displayItems = new List<PayUPaymentItem>();

            foreach (var item in cart.Items)
            {
                var taxRate = await _taxService.GetTaxRateAsync(item.Item.Product);
                var calculationContext = await _priceCalculationService.CreateCalculationContextAsync(item, calculationOptions);
                var (unitPrice, subtotal) = await _priceCalculationService.CalculateSubtotalAsync(calculationContext);

                displayItems.Add(new PayUPaymentItem
                {
                    Amount = _roundingHelper.ToSmallestCurrencyUnit(subtotal.FinalPrice),
                    Name = item.Item.Product.GetLocalized(x => x.Name),
                    Quantity = item.Item.Quantity
                });
            }

            PayUPaymentRequest payuPaymentRequest = new PayUPaymentRequest
            {
                Country = _services.WorkContext.WorkingLanguage.UniqueSeoCode.ToUpper(),
                Currency = sandboxCurrency ? "PLN" : currency.CurrencyCode.ToUpperInvariant(),
                Total = _roundingHelper.ToSmallestCurrencyUnit(processPaymentRequest.OrderTotal, currency),
                DisplayItems = displayItems,
                PayerEmail = customer.BillingAddress.Email,
                PayerFirstName = customer.BillingAddress.FirstName,
                PayerLastName = customer.BillingAddress.LastName,
                Description = description
            };

            return payuPaymentRequest;
        }

        public async Task<PayUPaymentResult> InitializePayment(PayUPaymentRequest request)
        {
            string accessToken = await _authorizationService.GetAccessTokenAsync();

            HttpClientHandler httpClientHandler = new HttpClientHandler();
            httpClientHandler.AllowAutoRedirect = false;
            using (HttpClient hc = new HttpClient(httpClientHandler, true))
            {
                hc.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                string webhookUrl = string.IsNullOrEmpty(sandboxWebhook) ? _services.StoreContext.CurrentStore.GetBaseUrl() : sandboxWebhook;
                webhookUrl = webhookUrl.TrimEnd('/') + "/payu/webhookhandler";

                HttpContent content = JsonContent.Create(new
                {
                    customerIp = "127.0.0.1",
                    notifyUrl = webhookUrl,
                    merchantPosId = posid,
                    description = request.Description,
                    currencyCode = request.Currency,
                    totalAmount = request.Total,
                    buyer = new
                    {
                        email = request.PayerEmail,
                        firstName = request.PayerFirstName,
                        lastName = request.PayerLastName,
                        language = request.Country.ToLowerInvariant()
                    },
                    //products = request.DisplayItems.Select(x => new
                    //{
                    //    name = x.Name,
                    //    unitPrice = x.Amount,
                    //    quantity = x.Quantity
                    //}).ToArray()
                });
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                HttpResponseMessage response = await hc.PostAsync(endpoint + "/api/v2_1/orders", content);

                string result = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == HttpStatusCode.Found)
                {
                    JsonNode data = JsonSerializer.Deserialize<JsonNode>(result);

                    PayUPaymentResult res = new PayUPaymentResult()
                    {
                        RedirectUri = data["redirectUri"].GetValue<string>(),
                        TransactionID = data["orderId"].GetValue<string>()
                    };
                    return res;
                }
                else
                {
                    Logger.Error("Received non 302 response during payment creation, original response:");
                    Logger.Error(result);
                    throw new ApplicationException("Received non 302 response during payment creation.");
                }
            }
        }

        public async Task<bool> VoidPayment(string transactionId)
        {
            string accessToken = await _authorizationService.GetAccessTokenAsync();

            HttpClientHandler httpClientHandler = new HttpClientHandler();
            httpClientHandler.AllowAutoRedirect = false;
            using (HttpClient hc = new HttpClient(httpClientHandler, true))
            {
                hc.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                HttpResponseMessage response = await hc.DeleteAsync(endpoint + "/api/v2_1/orders/" + transactionId);

                string result = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    JsonNode data = JsonSerializer.Deserialize<JsonNode>(result);
                    JsonNode status = data["status"];
                    if (status != null && status["statusCode"].GetValue<string>() == "SUCCESS")
                    {
                        return true;
                    }
                    else
                    {
                        Logger.Error("Received non SUCCESS response during voiding payment, original response:");
                        Logger.Error(result);
                        return false;
                    }
                }
                else
                {
                    Logger.Error("Received non 200 response during voiding payment, original response:");
                    Logger.Error(result);
                    return false;
                }
            }
        }

        public async Task<PayURefundResult> RefundPayment(string transactionId, Money? amountToRefund)
        {
            string accessToken = await _authorizationService.GetAccessTokenAsync();

            HttpClientHandler httpClientHandler = new HttpClientHandler();
            httpClientHandler.AllowAutoRedirect = false;
            using (HttpClient hc = new HttpClient(httpClientHandler, true))
            {
                hc.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                HttpContent content = JsonContent.Create(new
                {
                    refund = amountToRefund != null ? (object)new
                    {
                        description = (string)null,
                        amount = _roundingHelper.ToSmallestCurrencyUnit(amountToRefund.Value),
                    } : new
                    {
                        description = (string)null
                    }
                });
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                HttpResponseMessage response = await hc.PostAsync($"{endpoint}/api/v2_1/orders/{transactionId}/refunds", content);

                string result = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    JsonNode data = JsonSerializer.Deserialize<JsonNode>(result);
                    JsonNode status = data["status"];
                    if (status != null && status["statusCode"].GetValue<string>() == "SUCCESS")
                    {
                        JsonNode orderId = data["orderId"];

                        JsonNode refund = data["refund"];
                        JsonNode refundId = refund["refundId"];
                        JsonNode amount = refund["amount"];
                        return new PayURefundResult()
                        {
                            Success = true,
                            RefundedAmount = decimal.Parse(amount.GetValue<string>()),
                            TransactionID = orderId.GetValue<string>(),
                            RefundID = refundId.GetValue<string>()
                        };
                    }
                    else
                    {
                        Logger.Error("Received non SUCCESS response during refunding payment, original response:");
                        Logger.Error(result);
                        return new PayURefundResult() { Success = false };
                    }
                }
                else
                {
                    Logger.Error("Received non 200 response during refunding payment, original response:");
                    Logger.Error(result);
                    return new PayURefundResult() { Success = false };
                }
            }
        }
    }
}
