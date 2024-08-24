global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading.Tasks;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Identity;
using Smartstore.Core.Localization;
using Smartstore.Engine.Modularity;
using Smartstore.Http;
using Smartstore.PayU.Configuration;
using Smartstore.PayU.Providers;

namespace Smartstore.PayU
{
    internal class Module : ModuleBase, IConfigurable, ICookiePublisher
    {
        private readonly IPaymentService _paymentService;

        public Module(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        public Localizer T { get; set; } = NullLocalizer.Instance;

        public RouteInfo GetConfigurationRoute()
            => new("Configure", "PayUAdmin", new { area = "Admin" });

        public async Task<IEnumerable<CookieInfo>> GetCookieInfosAsync()
        {
            var store = Services.StoreContext.CurrentStore;
            bool isActiveStripe = await _paymentService.IsPaymentProviderActiveAsync(PayUProvider.SystemName, null, store.Id);

            if (isActiveStripe)
            {
                CookieInfo cookieInfo = new CookieInfo
                {
                    Name = T("Plugins.FriendlyName.Smartstore.PayU"),
                    Description = T("Plugins.Smartstore.PayU.CookieInfo"),
                    CookieType = CookieType.Required
                };

                return new List<CookieInfo> { cookieInfo }.AsEnumerable();
            }

            return null;
        }

        public override async Task InstallAsync(ModuleInstallationContext context)
        {
            await ImportLanguageResourcesAsync();
            await TrySaveSettingsAsync<PayUSettings>();

            await base.InstallAsync(context);
        }

        public override async Task UninstallAsync()
        {
            await DeleteLanguageResourcesAsync();
            await DeleteSettingsAsync<PayUSettings>();

            await base.UninstallAsync();
        }
    }
}