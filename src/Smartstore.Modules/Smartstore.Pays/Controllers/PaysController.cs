using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Common;
using Smartstore.Core.Data;
using Smartstore.Core.Identity;
using Smartstore.Pays.Configuration;
using Smartstore.Pays.Providers;
using Smartstore.Web.Controllers;

namespace Smartstore.Pays.Controllers
{
    public class PaysController : ModuleController
    {
        private readonly SmartDbContext _db;
        private readonly PaysSettings _settings;

        public PaysController(
            SmartDbContext db,
            PaysSettings settings
            )
        {
            _db = db;
            _settings = settings;
        }

        [HttpGet]
        [Route("pays/redirok")]
        public IActionResult PaymentResultOk()
        {
            return View("PaymentResultOk");
        }

        [HttpGet]
        [Route("pays/redirerr")]
        public IActionResult PaymentResultError()
        {
            string description = Request.Query["PaymentOrderStatusDescription"];

            description = HtmlEncoder.Default.Encode(description ?? "");

            return View("PaymentResultError", description);
        }

        [HttpGet]
        [Route("pays/webhookhandler"), WebhookEndpoint]
        public async Task<IActionResult> WebhookHandler()
        {
            try
            {
                string signatureHash = Request.Query["Hash"];
                string paymentOrderID = Request.Query["PaymentOrderID"];
                string merchantOrderNumber = Request.Query["MerchantOrderNumber"];
                string paymentOrderStatusID = Request.Query["PaymentOrderStatusID"];
                string currencyID = Request.Query["CurrencyID"];
                string amount = Request.Query["Amount"];
                string currencyBaseUnits = Request.Query["CurrencyBaseUnits"];

                IActionResult verificationResult = VerifySignature(signatureHash, paymentOrderID,
                    merchantOrderNumber, paymentOrderStatusID, currencyID, amount, currencyBaseUnits);

                if (verificationResult != null)
                {
                    return verificationResult;
                }

                int orderId = int.Parse(merchantOrderNumber);

                if (string.IsNullOrEmpty(paymentOrderStatusID))
                {
                    Logger.Error("Pays webhook: PaymentOrderStatusID is missing in request.");
                    return BadRequest();
                }
                if (string.IsNullOrEmpty(merchantOrderNumber))
                {
                    Logger.Error("Pays webhook: MerchantOrderNumber is missing in request.");
                    return BadRequest();
                }
                if (amount == null)
                {
                    Logger.Error("Pays webhook: Amount is missing in request.");
                    return BadRequest();
                }
                decimal totalAmount = decimal.Parse(amount);

                if (paymentOrderStatusID == "1")
                {
                    Order order = await GetOrderAsync(orderId);

                    if (order != null && order.PaymentStatus == PaymentStatus.Authorized)
                    {
                        order.PaymentStatus = PaymentStatus.Pending;
                        order.AuthorizationTransactionCode = paymentOrderID;

                        await _db.SaveChangesAsync();
                    }

                    return Ok();
                }
                else if (paymentOrderStatusID == "3")
                {
                    Order order = await GetOrderAsync(orderId);

                    if (order != null)
                    {
                        Currency orderCurrency = _db.Currencies.Where(c => c.CurrencyCode == order.CustomerCurrencyCode).First();
                        if (orderCurrency.CurrencyCode != currencyID)
                        {
                            Logger.Error("Pays webhook: Currency mismatch. Expected: " + orderCurrency.CurrencyCode + ", received: " + currencyID);
                            return Forbid("Received currency is not same as order currency.");
                        }

                        decimal minFactor = (decimal)Math.Pow(10, orderCurrency.RoundNumDecimals);
                        decimal convertedAmount = totalAmount / minFactor;

                        order.PaymentStatus = order.OrderTotal == convertedAmount ? PaymentStatus.Paid : PaymentStatus.Pending;
                        order.AuthorizationTransactionCode = paymentOrderID;
                        await _db.SaveChangesAsync();
                    }
                    else
                    {
                        Logger.Error("Order was not found for transaction id: " + merchantOrderNumber);
                        return BadRequest();
                    }
                }
                else if (paymentOrderStatusID == "2")
                {
                    Order order = await GetOrderAsync(orderId);

                    if (order != null)
                    {
                        order.PaymentStatus = PaymentStatus.Voided;
                        order.AuthorizationTransactionCode = paymentOrderID;

                        await _db.SaveChangesAsync();
                    }
                }
                else
                {
                    Logger.Error("Unhandled transaction status: {0}", paymentOrderStatusID);
                    return BadRequest();
                }

                return Ok();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return StatusCode(500);
            }
        }

        private async Task<Order> GetOrderAsync(int orderId)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(x =>
                        x.PaymentMethodSystemName == PaysProvider.SystemName &&
                        x.Id == orderId);

            if (order == null)
            {
                Logger.Error("Order was not found for transaction id: " + orderId);
                return null;
            }

            return order;
        }

        private IActionResult VerifySignature(string signatureHeader, string paymentOrderID, string merchantOrderNumber, string paymentOrderStatusID, string currencyID, string amount, string currencyBaseUnits)
        {
            if (string.IsNullOrEmpty(signatureHeader))
            {
                Logger.Error("Pays webhook: Hash parameter is missing in request.");
                return Unauthorized("Pays webhook: Hash parameter is missing in request.");
            }

            string key = _settings.ApiKey;
            string verificationString = paymentOrderID + merchantOrderNumber + paymentOrderStatusID +
                                        currencyID + amount + currencyBaseUnits;
            byte[] encodedPassword = new UTF8Encoding().GetBytes(verificationString);

            string result = MD5HMACEncode(verificationString, key);

            if (signatureHeader != result)
            {
                Logger.Error("Pays webhook: signature verification failed. Was " + signatureHeader + ", expected " + result);
                return Unauthorized("Pays webhook: signature verification failed.");
            }

            return null;
        }

        private string MD5HMACEncode(string data, string key)
        {
            using (var hmacMd5 = new HMACMD5(Encoding.UTF8.GetBytes(key)))
            {
                byte[] dataBytes = Encoding.UTF8.GetBytes(data);
                byte[] hashBytes = hmacMd5.ComputeHash(dataBytes);

                StringBuilder hashString = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    hashString.Append(b.ToString("x2"));
                }
                return hashString.ToString();
            }
        }
    }
}