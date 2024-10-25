global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading.Tasks;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Identity;
using Smartstore.Core.Localization;
using Smartstore.Engine.Modularity;
using Smartstore.Http;
using Smartstore.Pays.Configuration;
using Smartstore.Pays.Providers;

namespace Smartstore.Pays
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
            => new("Configure", "PaysAdmin", new { area = "Admin" });

        public async Task<IEnumerable<CookieInfo>> GetCookieInfosAsync()
        {
            var store = Services.StoreContext.CurrentStore;
            bool isActiveStripe = await _paymentService.IsPaymentProviderActiveAsync(PaysProvider.SystemName, null, store.Id);

            if (isActiveStripe)
            {
                CookieInfo cookieInfo = new CookieInfo
                {
                    Name = T("Plugins.FriendlyName.Smartstore.Pays"),
                    Description = T("Plugins.Smartstore.Pays.CookieInfo"),
                    CookieType = CookieType.Required
                };

                return new List<CookieInfo> { cookieInfo }.AsEnumerable();
            }

            return null;
        }

        public override async Task InstallAsync(ModuleInstallationContext context)
        {
            await ImportLanguageResourcesAsync();
            await TrySaveSettingsAsync<PaysSettings>();

            await base.InstallAsync(context);
        }

        public override async Task UninstallAsync()
        {
            await DeleteLanguageResourcesAsync();
            await DeleteSettingsAsync<PaysSettings>();

            await base.UninstallAsync();
        }
    }
}