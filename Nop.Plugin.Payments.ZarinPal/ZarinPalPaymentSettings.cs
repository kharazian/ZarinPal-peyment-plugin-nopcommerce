using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.ZarinPal
{
    public class ZarinPalPaymentSettings : ISettings
    {
        public string ZarinPalId { get; set; }
        public string BusinessPhoneNumber { get; set; }
        public bool UseSandbox { get; set; }
        public string BusinessEmail { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value.
        /// </summary>
        public bool AdditionalFeePercentage { get; set; }
        /// <summary>
        /// Additional fee
        /// </summary>
        public decimal AdditionalFee { get; set; }
        public bool PassProductNamesAndTotals { get; set; }
        public bool PdtValidateOrderTotal { get; set; }
        public bool ReturnFromZarinPalWithoutPaymentRedirectsToOrderDetailsPage { get; set; }
    }
}
