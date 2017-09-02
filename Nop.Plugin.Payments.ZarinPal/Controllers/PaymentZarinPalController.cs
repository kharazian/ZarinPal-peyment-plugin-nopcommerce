using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.ZarinPal.Models;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;
using System.ServiceModel;

namespace Nop.Plugin.Payments.ZarinPal.Controllers
{
    public class PaymentZarinPalController : BasePaymentController
    {
        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILocalizationService _localizationService;
        private readonly IStoreContext _storeContext;
        private readonly ILogger _logger;
        private readonly IWebHelper _webHelper;
        private readonly PaymentSettings _paymentSettings;
        private readonly ZarinPalPaymentSettings _zarinPalPaymentSettings;
        private readonly ShoppingCartSettings _shoppingCartSettings;

        public PaymentZarinPalController(IWorkContext workContext,
            IStoreService storeService, 
            ISettingService settingService, 
            IPaymentService paymentService, 
            IOrderService orderService, 
            IOrderProcessingService orderProcessingService,
            IGenericAttributeService genericAttributeService,
            ILocalizationService localizationService,
            IStoreContext storeContext,
            ILogger logger, 
            IWebHelper webHelper,
            PaymentSettings paymentSettings,
            ZarinPalPaymentSettings zarinPalPaymentSettings,
            ShoppingCartSettings shoppingCartSettings)
        {
            this._workContext = workContext;
            this._storeService = storeService;
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._genericAttributeService = genericAttributeService;
            this._localizationService = localizationService;
            this._storeContext = storeContext;
            this._logger = logger;
            this._webHelper = webHelper;
            this._paymentSettings = paymentSettings;
            this._zarinPalPaymentSettings = zarinPalPaymentSettings;
            this._shoppingCartSettings = shoppingCartSettings;
        }
        
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var ZarinPalPaymentSettings = _settingService.LoadSetting<ZarinPalPaymentSettings>(storeScope);

            var model = new ConfigurationModel();
            model.UseSandbox = ZarinPalPaymentSettings.UseSandbox;
            model.ZarinPalId = ZarinPalPaymentSettings.ZarinPalId;
            model.BusinessPhoneNumber = ZarinPalPaymentSettings.BusinessPhoneNumber;
            model.BusinessEmail = ZarinPalPaymentSettings.BusinessEmail;
            model.PdtValidateOrderTotal = ZarinPalPaymentSettings.PdtValidateOrderTotal;
            model.AdditionalFee = ZarinPalPaymentSettings.AdditionalFee;
            model.AdditionalFeePercentage = ZarinPalPaymentSettings.AdditionalFeePercentage;
            model.PassProductNamesAndTotals = ZarinPalPaymentSettings.PassProductNamesAndTotals;
            model.ReturnFromZarinPalWithoutPaymentRedirectsToOrderDetailsPage = ZarinPalPaymentSettings.ReturnFromZarinPalWithoutPaymentRedirectsToOrderDetailsPage;

            model.ActiveStoreScopeConfiguration = storeScope;
            if (storeScope > 0)
            {
                model.UseSandbox_OverrideForStore = _settingService.SettingExists(ZarinPalPaymentSettings, x => x.UseSandbox, storeScope);
                model.BusinessEmail_OverrideForStore = _settingService.SettingExists(ZarinPalPaymentSettings, x => x.BusinessEmail, storeScope);
                model.PdtValidateOrderTotal_OverrideForStore = _settingService.SettingExists(ZarinPalPaymentSettings, x => x.PdtValidateOrderTotal, storeScope);
                model.AdditionalFee_OverrideForStore = _settingService.SettingExists(ZarinPalPaymentSettings, x => x.AdditionalFee, storeScope);
                model.AdditionalFeePercentage_OverrideForStore = _settingService.SettingExists(ZarinPalPaymentSettings, x => x.AdditionalFeePercentage, storeScope);
                model.PassProductNamesAndTotals_OverrideForStore = _settingService.SettingExists(ZarinPalPaymentSettings, x => x.PassProductNamesAndTotals, storeScope);
                model.ReturnFromZarinPalWithoutPaymentRedirectsToOrderDetailsPage_OverrideForStore = _settingService.SettingExists(ZarinPalPaymentSettings, x => x.ReturnFromZarinPalWithoutPaymentRedirectsToOrderDetailsPage, storeScope);
            }

            return View("~/Plugins/Payments.ZarinPal/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var ZarinPalPaymentSettings = _settingService.LoadSetting<ZarinPalPaymentSettings>(storeScope);

            //save settings
            ZarinPalPaymentSettings.UseSandbox = model.UseSandbox;
            ZarinPalPaymentSettings.ZarinPalId = model.ZarinPalId;
            ZarinPalPaymentSettings.BusinessPhoneNumber = model.BusinessPhoneNumber;
            ZarinPalPaymentSettings.BusinessEmail = model.BusinessEmail;
            ZarinPalPaymentSettings.PdtValidateOrderTotal = model.PdtValidateOrderTotal;
            ZarinPalPaymentSettings.AdditionalFee = model.AdditionalFee;
            ZarinPalPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
            ZarinPalPaymentSettings.PassProductNamesAndTotals = model.PassProductNamesAndTotals;
            ZarinPalPaymentSettings.ReturnFromZarinPalWithoutPaymentRedirectsToOrderDetailsPage = model.ReturnFromZarinPalWithoutPaymentRedirectsToOrderDetailsPage;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            _settingService.SaveSettingOverridablePerStore(ZarinPalPaymentSettings, x => x.UseSandbox, model.UseSandbox_OverrideForStore, storeScope, false);            _settingService.SaveSettingOverridablePerStore(ZarinPalPaymentSettings, x => x.UseSandbox, model.UseSandbox_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(ZarinPalPaymentSettings, x => x.ZarinPalId, model.ZarinPalId_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(ZarinPalPaymentSettings, x => x.BusinessPhoneNumber, model.BusinessPhoneNumber_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(ZarinPalPaymentSettings, x => x.BusinessEmail, model.BusinessEmail_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(ZarinPalPaymentSettings, x => x.PdtValidateOrderTotal, model.PdtValidateOrderTotal_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(ZarinPalPaymentSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(ZarinPalPaymentSettings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(ZarinPalPaymentSettings, x => x.PassProductNamesAndTotals, model.PassProductNamesAndTotals_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(ZarinPalPaymentSettings, x => x.ReturnFromZarinPalWithoutPaymentRedirectsToOrderDetailsPage, model.ReturnFromZarinPalWithoutPaymentRedirectsToOrderDetailsPage_OverrideForStore, storeScope, false);
            
            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        //action displaying notification (warning) to a store owner about inaccurate ZarinPal rounding
        [ValidateInput(false)]
        public ActionResult RoundingWarning(bool passProductNamesAndTotals)
        {
            //prices and total aren't rounded, so display warning
            if (passProductNamesAndTotals && !_shoppingCartSettings.RoundPricesDuringCalculation)
                return Json(new { Result = _localizationService.GetResource("Plugins.Payments.ZarinPal.RoundingWarning") }, JsonRequestBehavior.AllowGet);

            return Json(new { Result = string.Empty }, JsonRequestBehavior.AllowGet);
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            return View("~/Plugins/Payments.ZarinPal/Views/PaymentInfo.cshtml");
        }

        [NonAction]
        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();
            return warnings;
        }

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            return paymentInfo;
        }

        [ValidateInput(false)]
        public ActionResult PDTHandler(FormCollection form)
        {
            var Status = _webHelper.QueryString<string>("Status");
            var Authority = _webHelper.QueryString<string>("Authority");          
            
            string response = _webHelper.GetThisPageUrl(true);
            
            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.ZarinPal") as ZarinPalPaymentProcessor;
            if (processor == null ||
                !processor.IsPaymentMethodActive(_paymentSettings) || !processor.PluginDescriptor.Installed)
                throw new NopException("ZarinPal module cannot be loaded");
            Dictionary<string, string> values = processor.GetPdtDetails(response);

            string orderNumber = string.Empty;
            values.TryGetValue("custom", out orderNumber);
            Guid orderNumberGuid = Guid.Empty;
            try
            {
                orderNumberGuid = new Guid(orderNumber);
            }
            catch { }
            Order order = _orderService.GetOrderByGuid(orderNumberGuid);
            var orderTotalSentToZarinPal = order.GetAttribute<decimal?>("OrderTotalSentToZarinPal");
            long RefID = 0;
            var payment_status = processor.GetPdtConfirm(Status, Authority, Convert.ToInt32(orderTotalSentToZarinPal),out RefID);

            if (payment_status == 100)
            {
                if (order != null)
                {

                    var sb = new StringBuilder();
                    sb.AppendLine("ZarinPal PDT:");
                    sb.AppendLine("payment_status: " + Status);
                    sb.AppendLine("payment_status_code: " + payment_status.ToString());


                    var newPaymentStatus = ZarinPalHelper.GetPaymentStatus(Status, payment_status);
                    sb.AppendLine("New payment status: " + newPaymentStatus);

                    //order note
                    order.OrderNotes.Add(new OrderNote
                    {
                        Note = sb.ToString(),
                        DisplayToCustomer = false,
                        CreatedOnUtc = DateTime.UtcNow
                    });
                    _orderService.UpdateOrder(order);                    

                    //validate order total
                    if (orderTotalSentToZarinPal.HasValue && orderTotalSentToZarinPal != order.OrderTotal)
                    {
                        string errorStr = string.Format("ZarinPal PDT. Returned order total doesn't equal order total {0}. Order# {1}.", order.OrderTotal, order.Id);
                        //log
                        _logger.Error(errorStr);
                        //order note
                        order.OrderNotes.Add(new OrderNote
                        {
                            Note = errorStr,
                            DisplayToCustomer = false,
                            CreatedOnUtc = DateTime.UtcNow
                        });
                        _orderService.UpdateOrder(order);

                        return RedirectToAction("Index", "Home", new { area = "" });
                    }
                    //clear attribute
                    if (orderTotalSentToZarinPal.HasValue)
                        _genericAttributeService.SaveAttribute<decimal?>(order, "OrderTotalSentToZarinPal", null);

                    _genericAttributeService.SaveAttribute<long?>(order, "OrderZarinPalRefId", RefID);
                    //mark order as paid
                    if (newPaymentStatus == PaymentStatus.Paid)
                    {
                        if (_orderProcessingService.CanMarkOrderAsPaid(order))
                        {
                            order.AuthorizationTransactionId = RefID.ToString();
                            _orderService.UpdateOrder(order);

                            _orderProcessingService.MarkOrderAsPaid(order);
                        }
                    }
                }

                return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id});
            }
            else
            {
                if (order != null)
                {
                    //order note
                    order.OrderNotes.Add(new OrderNote
                    {
                        Note = "ZarinPal PDT failed. " + response,
                        DisplayToCustomer = false,
                        CreatedOnUtc = DateTime.UtcNow
                    });
                    _orderService.UpdateOrder(order);
                }
                    return RedirectToRoute("Plugin.Payments.ZarinPal.CancelOrder");
            }
        }

        public ActionResult CancelOrder(FormCollection form)
        {
            if (_zarinPalPaymentSettings.ReturnFromZarinPalWithoutPaymentRedirectsToOrderDetailsPage)
            {
                var order = _orderService.SearchOrders(storeId: _storeContext.CurrentStore.Id,
                    customerId: _workContext.CurrentCustomer.Id, pageSize: 1)
                    .FirstOrDefault();
                if (order != null)
                {
                    return RedirectToRoute("OrderDetails", new { orderId = order.Id });
                }
            }

            return RedirectToAction("Index", "Home", new { area = "" });
        }
    }
}