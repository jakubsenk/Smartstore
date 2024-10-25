using Smartstore.Core.Configuration;

namespace Smartstore.Pays.Configuration
{
    public class PaysSettings : ISettings
    {
        /// <summary>
        /// Merchant. Used to authorize in pays system.
        /// </summary>
        public string Merchant { get; set; }
        /// <summary>
        /// Shop. Used to authorize in pays system.
        /// </summary>
        public string Shop { get; set; }

        /// <summary>
        /// Api key (MD5). Used for verification of incoming requests to webhook.
        /// </summary>
        public string ApiKey { get; set; }

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
