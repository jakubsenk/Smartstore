using Microsoft.AspNetCore.Mvc;
using Smartstore.ComponentModel;
using Smartstore.Core.Security;
using Smartstore.Engine.Modularity;
using Smartstore.Pays.Configuration;
using Smartstore.Pays.Models;
using Smartstore.Pays.Providers;
using Smartstore.Web.Controllers;
using Smartstore.Web.Modelling.Settings;

namespace Smartstore.Pays.Controllers
{
    [Area("Admin")]
    public class PaysAdminController : ModuleController
    {
        private readonly IProviderManager _providerManager;

        public PaysAdminController(IProviderManager providerManager)
        {
            _providerManager = providerManager;
        }

        [LoadSetting, AuthorizeAdmin]
        public IActionResult Configure(PaysSettings settings)
        {
            ViewBag.Provider = _providerManager.GetProvider(PaysProvider.SystemName).Metadata;

            ConfigurationModel model = MiniMapper.Map<PaysSettings, ConfigurationModel>(settings);

            ViewBag.CurrentCurrencyCode = Services.CurrencyService.PrimaryCurrency.CurrencyCode;

            return View(model);
        }

        [HttpPost, SaveSetting, AuthorizeAdmin]
        public IActionResult Configure(ConfigurationModel model, PaysSettings settings)
        {
            if (!ModelState.IsValid)
            {
                return Configure(settings);
            }

            ModelState.Clear();
            MiniMapper.Map(model, settings);

            return RedirectToAction(nameof(Configure));
        }
    }
}