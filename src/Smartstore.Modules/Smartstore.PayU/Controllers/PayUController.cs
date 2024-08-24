using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Data;
using Smartstore.Core.Identity;
using Smartstore.PayU.Configuration;
using Smartstore.PayU.Providers;
using Smartstore.Web.Controllers;

namespace Smartstore.PayU.Controllers
{
    public class PayUController : ModuleController
    {
        private readonly SmartDbContext _db;
        private readonly PayUSettings _settings;

        public PayUController(
            SmartDbContext db,
            PayUSettings settings
            )
        {
            _db = db;
            _settings = settings;
        }

        [HttpPost]
        [Route("payu/webhookhandler"), WebhookEndpoint]
        public async Task<IActionResult> WebhookHandler()
        {
            using StreamReader reader = new StreamReader(HttpContext.Request.Body);
            string json = await reader.ReadToEndAsync();

            try
            {
                var signatureHeader = Request.Headers["OpenPayU-Signature"];

                IActionResult verificationResult = VerifySignature(signatureHeader, json);
                if (verificationResult != null)
                {
                    return verificationResult;
                }

                JsonNode payuResponse = JsonNode.Parse(json);
                JsonNode orderNode = payuResponse["order"];
                if (orderNode == null)
                {
                    JsonNode refundNode = payuResponse["refund"];
                    JsonNode refundOrderId = payuResponse["orderId"];

                    if (refundNode == null || refundOrderId == null)
                    {
                        Logger.Error("PayU webhook: order or refund are missing in response.");
                        return BadRequest();
                    }
                    JsonNode refundStatus = refundNode["status"];
                    if (refundStatus == null)
                    {
                        Logger.Error("PayU webhook: status is missing in response.");
                        return BadRequest();
                    }
                    Order order = await GetOrderAsync(refundOrderId.GetValue<string>());

                    string refundId = refundNode["refundId"].GetValue<string>();
                    if (refundStatus.GetValue<string>() != "FINALIZED")
                    {
                        Logger.Error("PayU webhook: refund failed.");
                        order.AddOrderNote("Refund was not successfull. PayU response: " + json);
                    }
                    else
                    {
                        order.AddOrderNote("Order was refunded by PayU.");
                    }
                    await _db.SaveChangesAsync();

                    return Ok();
                }
                string status = orderNode["status"].GetValue<string>();
                if (status == null)
                {
                    Logger.Error("PayU webhook: status is missing in response.");
                    return BadRequest();
                }
                JsonNode orderId = orderNode["orderId"];
                if (orderId == null)
                {
                    Logger.Error("PayU webhook: orderId is missing in response.");
                    return BadRequest();
                }
                string totalAmountStr = orderNode["totalAmount"].GetValue<string>();
                if (totalAmountStr == null)
                {
                    Logger.Error("PayU webhook: totalAmount is missing in response.");
                    return BadRequest();
                }
                decimal totalAmount = decimal.Parse(totalAmountStr);

                if (status == "PENDING")
                {
                    Order order = await GetOrderAsync(orderId.GetValue<string>());

                    if (order != null && order.PaymentStatus == PaymentStatus.Authorized)
                    {
                        order.PaymentStatus = PaymentStatus.Pending;

                        await _db.SaveChangesAsync();
                    }

                    return Ok();
                }
                else if (status == "COMPLETED")
                {
                    Order order = await GetOrderAsync(orderId.GetValue<string>());

                    if (order != null)
                    {
                        decimal convertedAmount = totalAmount / 100M;

                        order.PaymentStatus = order.OrderTotal == convertedAmount ? PaymentStatus.Paid : PaymentStatus.Pending;

                        await _db.SaveChangesAsync();
                    }
                }
                else if (status == "CANCELED")
                {
                    Order order = await GetOrderAsync(orderId.GetValue<string>());

                    if (order != null)
                    {
                        order.PaymentStatus = PaymentStatus.Voided;

                        await _db.SaveChangesAsync();
                    }
                }
                else
                {
                    Logger.Warn("Unhandled transaction status: {0}", status);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return StatusCode(500);
            }
        }

        private async Task<Order> GetOrderAsync(string paymentIntentId)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(x =>
                        x.PaymentMethodSystemName == PayUProvider.SystemName &&
                        x.AuthorizationTransactionId == paymentIntentId);

            if (order == null)
            {
                Logger.Error("Order was not found for transaction id: " + paymentIntentId);
                return null;
            }

            return order;
        }

        private IActionResult VerifySignature(string signatureHeader, string json)
        {
            if (string.IsNullOrEmpty(signatureHeader))
            {
                Logger.Error("PayU webhook: OpenPayU-Signature header is missing in request.");
                return Unauthorized("PayU webhook: OpenPayU-Signature header is missing in request.");
            }

            string[] signatureParts = signatureHeader.ToString().Split(";");
            string signature = signatureParts.FirstOrDefault(s => s.StartsWith("signature="));
            if (string.IsNullOrEmpty(signature))
            {
                Logger.Error("PayU webhook: signature is missing in request.");
                return Unauthorized("PayU webhook: signature is missing in request.");
            }
            signature = signature.Substring("signature=".Length);
            string algorithm = signatureParts.FirstOrDefault(s => s.StartsWith("algorithm="));
            if (string.IsNullOrEmpty(algorithm))
            {
                Logger.Error("PayU webhook: algorithm is missing in request.");
                return Unauthorized("PayU webhook: algorithm is missing in request.");
            }
            algorithm = algorithm.Substring("algorithm=".Length);
            if (algorithm != "MD5")
            {
                Logger.Error("PayU webhook: unsupported algorithm: {0}", algorithm);
                return Unauthorized();
            }
            string secondKey = _settings.IsSandbox ? _settings.SandboxSecondKey : _settings.SecondKey;
            string verificationString = json + secondKey;
            byte[] encodedPassword = new UTF8Encoding().GetBytes(verificationString);

            byte[] hash = ((HashAlgorithm)CryptoConfig.CreateFromName("MD5")).ComputeHash(encodedPassword);

            string result = BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();

            if (signature != result)
            {
                Logger.Error("PayU webhook: signature verification failed.");
                return Unauthorized("PayU webhook: signature verification failed.");
            }

            return null;
        }
    }
}