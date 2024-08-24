using Smartstore.Web.Modelling;

namespace Smartstore.PayU.Models
{
    [LocalizedDisplay("Plugins.Smartstore.PayU.")]
    public class ConfigurationModel : ModelBase
    {
        [LocalizedDisplay("*IsSandbox")]
        public bool IsSandbox { get; set; }

        [LocalizedDisplay("*Description")]
        public string Description { get; set; }


        [LocalizedDisplay("*ClientID")]
        public string ClientID { get; set; }

        [LocalizedDisplay("*PosID")]
        public string PosID { get; set; }

        [LocalizedDisplay("*ClientSecret")]
        public string ClientSecret { get; set; }

        [LocalizedDisplay("*Endpoint")]
        public string Endpoint => "https://secure.payu.com";

        [LocalizedDisplay("*Region")]
        public string Region => "pl";

        [LocalizedDisplay("*SecondKey")]
        public string SecondKey { get; set; }




        [LocalizedDisplay("*SandboxClientID")]
        public string SandboxClientID { get; set; }

        [LocalizedDisplay("*SandboxPosID")]
        public string SandboxPosID { get; set; }

        [LocalizedDisplay("*SandboxClientSecret")]
        public string SandboxClientSecret { get; set; }

        [LocalizedDisplay("*SandboxEndpoint")]
        public string SandboxEndpoint => "https://secure.snd.payu.com";

        [LocalizedDisplay("*SandboxRegion")]
        public string SandboxRegion => "pl";

        [LocalizedDisplay("*SandboxWebhook")]
        public string SandboxWebhook { get; set; }

        [LocalizedDisplay("*SecondKey")]
        public string SandboxSecondKey { get; set; }



        [LocalizedDisplay("*AdditionalFee")]
        public decimal AdditionalFee { get; set; }

        [LocalizedDisplay("*AdditionalFeePercentage")]
        public bool AdditionalFeePercentage { get; set; }
    }
}