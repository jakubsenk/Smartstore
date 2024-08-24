using Microsoft.AspNetCore.Mvc;
using Smartstore.ComponentModel;
using Smartstore.Core.Security;
using Smartstore.Engine.Modularity;
using Smartstore.PayU.Configuration;
using Smartstore.PayU.Models;
using Smartstore.PayU.Providers;
using Smartstore.PayU.Services;
using Smartstore.Web.Controllers;
using Smartstore.Web.Modelling.Settings;

namespace Smartstore.PayU.Controllers
{
    [Area("Admin")]
    public class PayUAdminController : ModuleController
    {
        private readonly IProviderManager _providerManager;
        private readonly PayUGateAuthorizationService _authService;

        public PayUAdminController(IProviderManager providerManager, PayUGateAuthorizationService authService)
        {
            _providerManager = providerManager;
            _authService = authService;
        }

        [LoadSetting, AuthorizeAdmin]
        public IActionResult Configure(PayUSettings settings)
        {
            ViewBag.Provider = _providerManager.GetProvider(PayUProvider.SystemName).Metadata;

            ConfigurationModel model = MiniMapper.Map<PayUSettings, ConfigurationModel>(settings);

            ViewBag.CurrentCurrencyCode = Services.CurrencyService.PrimaryCurrency.CurrencyCode;

            return View(model);
        }

        [HttpPost, SaveSetting, AuthorizeAdmin]
        public IActionResult Configure(ConfigurationModel model, PayUSettings settings)
        {
            if (!ModelState.IsValid)
            {
                return Configure(settings);
            }

            ModelState.Clear();
            MiniMapper.Map(model, settings);

            _authService.UpdateConfiguration(settings);

            return RedirectToAction(nameof(Configure));
        }
    }
}