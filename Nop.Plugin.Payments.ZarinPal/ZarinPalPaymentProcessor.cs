﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Routing;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.ZarinPal.Controllers;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Tax;
using System.ServiceModel;
using Nop.Plugin.Payments.ZarinPal.wsZarinPal;

namespace Nop.Plugin.Payments.ZarinPal
{
    /// <summary>
    /// ZarinPal payment processor
    /// </summary>
    public class ZarinPalPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Constants

        /// <summary>
        /// nopCommerce partner code
        /// </summary>
        private const string BN_CODE = "nopCommerce_SP";

        #endregion

        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly HttpContextBase _httpContext;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ICurrencyService _currencyService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILocalizationService _localizationService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly ISettingService _settingService;
        private readonly ITaxService _taxService;
        private readonly IWebHelper _webHelper;
        private readonly ZarinPalPaymentSettings _zarinPalPaymentSettings;
        private readonly IStoreContext _storeContext;

        #endregion

        #region Ctor

        public ZarinPalPaymentProcessor(CurrencySettings currencySettings,
            HttpContextBase httpContext,
            ICheckoutAttributeParser checkoutAttributeParser,
            ICurrencyService currencyService,
            IGenericAttributeService genericAttributeService,
            ILocalizationService localizationService,
            IOrderTotalCalculationService orderTotalCalculationService,
            ISettingService settingService,
            ITaxService taxService,
            IWebHelper webHelper,
            ZarinPalPaymentSettings zarinPalPaymentSettings,
            IStoreContext storeContext)
        {
            this._currencySettings = currencySettings;
            this._httpContext = httpContext;
            this._checkoutAttributeParser = checkoutAttributeParser;
            this._currencyService = currencyService;
            this._genericAttributeService = genericAttributeService;
            this._localizationService = localizationService;
            this._orderTotalCalculationService = orderTotalCalculationService;
            this._settingService = settingService;
            this._taxService = taxService;
            this._webHelper = webHelper;
            this._zarinPalPaymentSettings = zarinPalPaymentSettings;
            this._storeContext = storeContext;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Gets ZarinPal URL
        /// </summary>
        /// <returns></returns>
        private string GetZarinPalUrl()
        {
            return _zarinPalPaymentSettings.UseSandbox ? "https://sandbox.zarinpal.com/pg/services/WebGate/service" : "https://www.zarinpal.com/pg/services/WebGate/service";
        }
        private string GetZarinPalRedirect()
        {
            return _zarinPalPaymentSettings.UseSandbox ? "https://sandbox.zarinpal.com/pg/StartPay/" : "https://www.zarinpal.com/pg/StartPay/";
        }
        public int GetPdtConfirm(string status, string authority, int amount, out long refID)
        {
            long RefID = 0;
            int StatusRe = 0;

            if (status != null && status != "")
            {
                if (status.Equals("OK"))
                {
                    try
                    {
                        StatusRe = GetZarinPalService().PaymentVerification(_zarinPalPaymentSettings.ZarinPalId, authority, amount, out RefID);
                    }
                    catch { }
                }
            }
            refID = RefID;
            return StatusRe;
        }
        /// <summary>
        /// Gets PDT details
        /// </summary>
        /// <param name="tx">TX</param>
        /// <param name="values">Values</param>
        /// <param name="response">Response</param>
        /// <returns>Result</returns>
        public Dictionary<string, string> GetPdtDetails(string response)
        {
            bool firstLine = true;

            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string l in response.Split('&'))
            {
                string line = l.Trim();
                if (firstLine)
                {
                    firstLine = false;
                }
                else
                {
                    int equalPox = line.IndexOf('=');
                    if (equalPox >= 0)
                        values.Add(line.Substring(0, equalPox), line.Substring(equalPox + 1));
                }
            }
            return values;
        }

        /// <summary>
        /// Generate string (URL) for redirection
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        /// <param name="passProductNamesAndTotals">A value indicating whether to pass product names and totals</param>
        private string GenerationRedirectionUrl(PostProcessPaymentRequest postProcessPaymentRequest, bool passProductNamesAndTotals)
        {
            var builder = new StringBuilder();
            builder.Append(_webHelper.GetStoreLocation(false) + "Plugins/PaymentZarinPal/PDTHandler");
            var cmd = passProductNamesAndTotals
                ? "_cart"
                : "_xclick";
            builder.AppendFormat("?cmd={0}", cmd);
            if (passProductNamesAndTotals)
            {
                builder.AppendFormat("&upload=1");

                //get the items in the cart
                decimal cartTotal = decimal.Zero;
                var cartTotalRounded = decimal.Zero;
                var cartItems = postProcessPaymentRequest.Order.OrderItems;
                int x = 1;
                foreach (var item in cartItems)
                {
                    var unitPriceExclTax = item.UnitPriceExclTax;
                    var priceExclTax = item.PriceExclTax;
                    //round
                    var unitPriceExclTaxRounded = Math.Round(unitPriceExclTax, 2);
                    builder.AppendFormat("&item_name_" + x + "={0}", HttpUtility.UrlEncode(item.Product.Name));
                    builder.AppendFormat("&amount_" + x + "={0}", unitPriceExclTaxRounded.ToString("0.00", CultureInfo.InvariantCulture));
                    builder.AppendFormat("&quantity_" + x + "={0}", item.Quantity);
                    x++;
                    cartTotal += priceExclTax;
                    cartTotalRounded += unitPriceExclTaxRounded * item.Quantity;
                }

                //the checkout attributes that have a cost value and send them to ZarinPal as items to be paid for
                var attributeValues = _checkoutAttributeParser.ParseCheckoutAttributeValues(postProcessPaymentRequest.Order.CheckoutAttributesXml);
                foreach (var val in attributeValues)
                {
                    var attPrice = _taxService.GetCheckoutAttributePrice(val, false, postProcessPaymentRequest.Order.Customer);
                    //round
                    var attPriceRounded = Math.Round(attPrice, 2);
                    if (attPrice > decimal.Zero) //if it has a price
                    {
                        var attribute = val.CheckoutAttribute;
                        if (attribute != null)
                        {
                            var attName = attribute.Name; //set the name
                            builder.AppendFormat("&item_name_" + x + "={0}", HttpUtility.UrlEncode(attName)); //name
                            builder.AppendFormat("&amount_" + x + "={0}", attPriceRounded.ToString("0.00", CultureInfo.InvariantCulture)); //amount
                            builder.AppendFormat("&quantity_" + x + "={0}", 1); //quantity
                            x++;
                            cartTotal += attPrice;
                            cartTotalRounded += attPriceRounded;
                        }
                    }
                }

                //order totals

                //shipping
                var orderShippingExclTax = postProcessPaymentRequest.Order.OrderShippingExclTax;
                var orderShippingExclTaxRounded = Math.Round(orderShippingExclTax, 2);
                if (orderShippingExclTax > decimal.Zero)
                {
                    builder.AppendFormat("&item_name_" + x + "={0}", "Shipping fee");
                    builder.AppendFormat("&amount_" + x + "={0}", orderShippingExclTaxRounded.ToString("0.00", CultureInfo.InvariantCulture));
                    builder.AppendFormat("&quantity_" + x + "={0}", 1);
                    x++;
                    cartTotal += orderShippingExclTax;
                    cartTotalRounded += orderShippingExclTaxRounded;
                }

                //payment method additional fee
                var paymentMethodAdditionalFeeExclTax = postProcessPaymentRequest.Order.PaymentMethodAdditionalFeeExclTax;
                var paymentMethodAdditionalFeeExclTaxRounded = Math.Round(paymentMethodAdditionalFeeExclTax, 2);
                if (paymentMethodAdditionalFeeExclTax > decimal.Zero)
                {
                    builder.AppendFormat("&item_name_" + x + "={0}", "Payment method fee");
                    builder.AppendFormat("&amount_" + x + "={0}", paymentMethodAdditionalFeeExclTaxRounded.ToString("0.00", CultureInfo.InvariantCulture));
                    builder.AppendFormat("&quantity_" + x + "={0}", 1);
                    x++;
                    cartTotal += paymentMethodAdditionalFeeExclTax;
                    cartTotalRounded += paymentMethodAdditionalFeeExclTaxRounded;
                }

                //tax
                var orderTax = postProcessPaymentRequest.Order.OrderTax;
                var orderTaxRounded = Math.Round(orderTax, 2);
                if (orderTax > decimal.Zero)
                {
                    //builder.AppendFormat("&tax_1={0}", orderTax.ToString("0.00", CultureInfo.InvariantCulture));

                    //add tax as item
                    builder.AppendFormat("&item_name_" + x + "={0}", HttpUtility.UrlEncode("Sales Tax")); //name
                    builder.AppendFormat("&amount_" + x + "={0}", orderTaxRounded.ToString("0.00", CultureInfo.InvariantCulture)); //amount
                    builder.AppendFormat("&quantity_" + x + "={0}", 1); //quantity

                    cartTotal += orderTax;
                    cartTotalRounded += orderTaxRounded;
                    x++;
                }

                if (cartTotal > postProcessPaymentRequest.Order.OrderTotal)
                {
                    /* Take the difference between what the order total is and what it should be and use that as the "discount".
                     * The difference equals the amount of the gift card and/or reward points used. 
                     */
                    decimal discountTotal = cartTotal - postProcessPaymentRequest.Order.OrderTotal;
                    discountTotal = Math.Round(discountTotal, 2);
                    cartTotalRounded -= discountTotal;
                    //gift card or rewared point amount applied to cart in nopCommerce - shows in ZarinPal as "discount"
                    builder.AppendFormat("&discount_amount_cart={0}", discountTotal.ToString("0.00", CultureInfo.InvariantCulture));
                }

                //save order total that actually sent to ZarinPal (used for PDT order total validation)
                _genericAttributeService.SaveAttribute(postProcessPaymentRequest.Order, "OrderTotalSentToZarinPal", cartTotalRounded);
            }
            else
            {
                //pass order total
                builder.AppendFormat("&item_name=Order Number {0}", postProcessPaymentRequest.Order.Id);
                var orderTotal = Math.Round(postProcessPaymentRequest.Order.OrderTotal, 2);
                builder.AppendFormat("&amount={0}", orderTotal.ToString("0.00", CultureInfo.InvariantCulture));

                //save order total that actually sent to ZarinPal (used for PDT order total validation)
                _genericAttributeService.SaveAttribute(postProcessPaymentRequest.Order, "OrderTotalSentToZarinPal", orderTotal);
            }

            builder.AppendFormat("&custom={0}", postProcessPaymentRequest.Order.OrderGuid);
            builder.AppendFormat("&charset={0}", "utf-8");
            builder.AppendFormat("&bn={0}", BN_CODE);
            builder.Append(string.Format("&no_note=1&currency_code={0}", HttpUtility.UrlEncode(_currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId).CurrencyCode)));
            builder.AppendFormat("&invoice={0}", postProcessPaymentRequest.Order.Id);
            builder.AppendFormat("&rm=2", new object[0]);
            if (postProcessPaymentRequest.Order.ShippingStatus != ShippingStatus.ShippingNotRequired)
                builder.AppendFormat("&no_shipping=2", new object[0]);
            else
                builder.AppendFormat("&no_shipping=1", new object[0]);

            return builder.ToString();
        }
        private wsZarinPal.PaymentGatewayImplementationServicePortTypeClient GetZarinPalService()
        {
            System.Net.ServicePointManager.Expect100Continue = false;
            BasicHttpsBinding binding = new BasicHttpsBinding();
            EndpointAddress endpoint = new EndpointAddress(new Uri(GetZarinPalUrl()));

            return new PaymentGatewayImplementationServicePortTypeClient(binding
                , endpoint);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.NewPaymentStatus = PaymentStatus.Pending;
            return result;
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var urlToRedirect = GenerationRedirectionUrl(postProcessPaymentRequest, _zarinPalPaymentSettings.PassProductNamesAndTotals);
            if (urlToRedirect == null)
                throw new Exception("ZarinPal URL cannot be generated");
            //ensure URL doesn't exceed 2K chars. Otherwise, customers can get "too long URL" exception
            if (urlToRedirect.Length > 2048)
                urlToRedirect = GenerationRedirectionUrl(postProcessPaymentRequest, false);

            string Authority = string.Empty;

            int Status = 0;
            try
            {
                Status = GetZarinPalService().PaymentRequest(_zarinPalPaymentSettings.ZarinPalId, Convert.ToInt32(postProcessPaymentRequest.Order.OrderTotal), this._storeContext.CurrentStore.Name, _zarinPalPaymentSettings.BusinessEmail, _zarinPalPaymentSettings.BusinessPhoneNumber, urlToRedirect, out Authority);
            }
            catch {}

            if (Status == 100)
            {
                _httpContext.Response.Redirect(GetZarinPalRedirect() + Authority);
            }
            else
            {
                _httpContext.Response.RedirectToRoute("HomePage");
            }

        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            var result = this.CalculateAdditionalFee(_orderTotalCalculationService, cart,
                _zarinPalPaymentSettings.AdditionalFee, _zarinPalPaymentSettings.AdditionalFeePercentage);
            return result;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return result;
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return result;
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return result;
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");
            
            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return false;

            return true;
        }

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentZarinPal";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.ZarinPal.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Gets a route for payment info
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentZarinPal";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.ZarinPal.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Get type of controller
        /// </summary>
        /// <returns>Type</returns>
        public Type GetControllerType()
        {
            return typeof(PaymentZarinPalController);
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            var settings = new ZarinPalPaymentSettings
            {
                ZarinPalId = "ZarinPal Getway Id",                
                UseSandbox = true,
                BusinessEmail = "test@test.com",
                PdtValidateOrderTotal = true,
            };
            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.AdditionalFee", "هزینه های مازاد");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.AdditionalFee.Hint", "مبلغ هزینه مازاد جهت درج در فاکتور مشتری.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.ZarinPalId", "شماره حساب زرین پال");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.ZarinPalId.Hint", "فعال کردن شماره حساب زرین پال");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.BusinessPhoneNumber", "شماره تلفن فروشگاه");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.BusinessPhoneNumber.Hint", "فعال کردن شماره تلفن فروشگاه");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.AdditionalFeePercentage", "هزینه مازاد بر اساس درصد");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.AdditionalFeePercentage.Hint", "آیا درصد هزینه مازاد برای کل فاکتور حساب شود؟ اگر این گزینه تیک نخورد هزینه مازاد بر اساس مقدار ثابت محاسبه خواهد شد.");

            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.BusinessEmail", "پست الکترونیک");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.BusinessEmail.Hint", "استفاده از پست الکترونیک اختصاصی.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.PassProductNamesAndTotals", "ارسال نام و مبلغ کالا برای زرین پال");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.PassProductNamesAndTotals.Hint", "فعال کردن ارسال نام و مبلغ کالا برای زرین پال");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.PDTValidateOrderTotal", "بررسی کالاها در زمان تایید پرداخت");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.PDTValidateOrderTotal.Hint", "فعال کردن بررسی کالاها در زمان تایید پرداخت");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.RedirectionTip", "شما برای نهایی کردن خرید و پرداخت فاکتور خود به سایت زرین پال منتقل خواهید شد.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.ReturnFromZarinPalWithoutPaymentRedirectsToOrderDetailsPage", "برگشت به صفحه خرید");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.ReturnFromZarinPalWithoutPaymentRedirectsToOrderDetailsPage.Hint", "فعال کردن برگشت به صفحه خرید در صورت کلیک بر روی لینگ \"برگشت به صفحه خرید\"");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.UseSandbox", "استفاده به صورت تست");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.UseSandbox.Hint", "فعال کردن محیط تست");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.Instructions", "<p><b>در صورت استفاده از این افزونه خواهشمند است شرایط استفاده از زرین پال را مطالعه فرمایید.</b><br /><br />برای استفاده از این افزونه باید شماره حساب زرین پال دریافت نمایید:<br /><br />1. وارد اکانت زرین پال شوید (اینجا <a href=\"https://www.ZarinPal.com/us/webapps/mpp/referral/ZarinPal-business-account2?partner_id=9JJPJNNPQ7PZ8\" target=\"_blank\">ثبت نام</a> ).<br /></p>");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.PaymentMethodDescription", "برای نهایی کردن خرید و پرداخت فاکتور به سایت زرین پال منتقل خواهید شد.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.ZarinPal.RoundingWarning", "گرد کردن مبلغ فاکتور.");

            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<ZarinPalPaymentSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.AdditionalFee");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.AdditionalFee.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.ZarinPalId");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.ZarinPalId.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.BusinessPhoneNumber");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.BusinessPhoneNumber.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.AdditionalFeePercentage");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.AdditionalFeePercentage.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.BusinessEmail");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.BusinessEmail.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.PassProductNamesAndTotals");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.PassProductNamesAndTotals.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.PDTValidateOrderTotal");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.PDTValidateOrderTotal.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.RedirectionTip");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.ReturnFromZarinPalWithoutPaymentRedirectsToOrderDetailsPage");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.ReturnFromZarinPalWithoutPaymentRedirectsToOrderDetailsPage.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.UseSandbox");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.Fields.UseSandbox.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.Instructions");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.PaymentMethodDescription");
            this.DeletePluginLocaleResource("Plugins.Payments.ZarinPal.RoundingWarning");

            base.Uninstall();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get { return RecurringPaymentType.NotSupported; }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get { return PaymentMethodType.Redirection; }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription
        {
            //return description of this payment method to be display on "payment method" checkout step. good practice is to make it localizable
            //for example, for a redirection payment method, description may be like this: "You will be redirected to ZarinPal site to complete the payment"
            get { return _localizationService.GetResource("Plugins.Payments.ZarinPal.PaymentMethodDescription"); }
        }

        #endregion
    }
}
