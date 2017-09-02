using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.ZarinPal.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.ZarinPal.Fields.UseSandbox")]
        public bool UseSandbox { get; set; }
        public bool UseSandbox_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.ZarinPal.Fields.ZarinPalId")]
        public string ZarinPalId { get; set; }
        public bool ZarinPalId_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.ZarinPal.Fields.BusinessPhoneNumber")]
        public string BusinessPhoneNumber { get; set; }
        public bool BusinessPhoneNumber_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.ZarinPal.Fields.BusinessEmail")]
        public string BusinessEmail { get; set; }
        public bool BusinessEmail_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.ZarinPal.Fields.PDTValidateOrderTotal")]
        public bool PdtValidateOrderTotal { get; set; }
        public bool PdtValidateOrderTotal_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.ZarinPal.Fields.AdditionalFee")]
        public decimal AdditionalFee { get; set; }
        public bool AdditionalFee_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.ZarinPal.Fields.AdditionalFeePercentage")]
        public bool AdditionalFeePercentage { get; set; }
        public bool AdditionalFeePercentage_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.ZarinPal.Fields.PassProductNamesAndTotals")]
        public bool PassProductNamesAndTotals { get; set; }
        public bool PassProductNamesAndTotals_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.ZarinPal.Fields.ReturnFromZarinPalWithoutPaymentRedirectsToOrderDetailsPage")]
        public bool ReturnFromZarinPalWithoutPaymentRedirectsToOrderDetailsPage { get; set; }
        public bool ReturnFromZarinPalWithoutPaymentRedirectsToOrderDetailsPage_OverrideForStore { get; set; }
    }
}