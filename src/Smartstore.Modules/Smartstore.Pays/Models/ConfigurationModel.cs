using Smartstore.Web.Modelling;

namespace Smartstore.Pays.Models
{
    [LocalizedDisplay("Plugins.Smartstore.Pays.")]
    public class ConfigurationModel : ModelBase
    {
        [LocalizedDisplay("*Merchant")]
        public string Merchant { get; set; }

        [LocalizedDisplay("*Shop")]
        public string Shop { get; set; }

        [LocalizedDisplay("*ApiKey")]
        public string ApiKey { get; set; }


        [LocalizedDisplay("*AdditionalFee")]
        public decimal AdditionalFee { get; set; }

        [LocalizedDisplay("*AdditionalFeePercentage")]
        public bool AdditionalFeePercentage { get; set; }
    }
}