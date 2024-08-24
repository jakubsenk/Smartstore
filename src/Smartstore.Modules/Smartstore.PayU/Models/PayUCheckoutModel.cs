namespace Smartstore.PayU.Models
{
    public class PayUPaymentRequest
    {
        public string Country { get; set; }

        public string Currency { get; set; }

        public string PayerFirstName { get; set; }
        public string PayerLastName { get; set; }

        public string PayerEmail { get; set; }
        public string Description { get; set; }

        public decimal Total { get; set; }

        public List<PayUPaymentItem> DisplayItems { get; set; } = new();
    }

    public class PayUPaymentItem
    {
        public string Name { get; set; }

        public int Amount { get; set; }

        public int Quantity { get; set; }
    }

    public class PayUPaymentResult
    {
        public string TransactionID { get; set; }
        public string RedirectUri { get; set; }
    }

    public class PayURefundResult
    {
        public string TransactionID { get; set; }
        public string RefundID { get; set; }
        public decimal RefundedAmount { get; set; }
        public bool Success { get; set; }
    }
}