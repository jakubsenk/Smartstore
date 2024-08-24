using Smartstore.Core.Configuration;

namespace Smartstore.PayU.Configuration
{
    public class PayUSettings : ISettings
    {
        /// <summary>
        /// Wheter to use sandbox credentials instead of the real ones.
        /// </summary>
        public bool IsSandbox { get; set; }
        /// <summary>
        /// Short description of the transaction displayed in confirmation email from payu.
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// ClientID. Used to authorize in payu system.
        /// </summary>
        public string ClientID { get; set; }
        /// <summary>
        /// PosID. Used to authorize in payu system.
        /// </summary>
        public string PosID { get; set; }

        /// <summary>
        /// Secret Api key. Used for server to server communication.
        /// </summary>
        public string ClientSecret { get; set; }
        public string Endpoint { get; set; }
        public string Region { get; set; }
        /// <summary>
        /// Second key (MD5). Used for verification of incoming requests to webhook.
        /// </summary>
        public string SecondKey { get; set; }


        public string SandboxClientID { get; set; }
        public string SandboxPosID { get; set; }
        public string SandboxClientSecret { get; set; }
        public string SandboxEndpoint { get; set; }
        public string SandboxRegion { get; set; }
        public string SandboxWebhook { get; set; }
        public string SandboxSecondKey { get; set; }

        /// <summary>
        /// Specifies the additional handling fee charged to the customer when using this method.
        /// </summary>
        public decimal AdditionalFee { get; set; }

        /// <summary>
        /// Specifies whether the addional fee should be a percentage value based on the current cart.
        /// </summary>
        public bool AdditionalFeePercentage { get; set; }
    }
}
