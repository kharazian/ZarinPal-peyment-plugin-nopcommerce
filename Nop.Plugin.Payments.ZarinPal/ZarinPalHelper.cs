using Nop.Core.Domain.Payments;

namespace Nop.Plugin.Payments.ZarinPal
{
    /// <summary>
    /// Represents paypal helper
    /// </summary>
    public class ZarinPalHelper
    {
        /// <summary>
        /// Gets a payment status
        /// </summary>
        /// <param name="paymentStatus">PayPal payment status</param>
        /// <param name="pendingReason">PayPal pending reason</param>
        /// <returns>Payment status</returns>
        public static PaymentStatus GetPaymentStatus(string Status, int payment_status)
        {
            var result = PaymentStatus.Pending;

            if (Status == null)
                Status = string.Empty;

            switch (Status.ToLowerInvariant())
            {
                case "ok":
                    switch (payment_status)
                    {
                        case 100:
                            result = PaymentStatus.Paid;
                            break;
                        default:
                            result = PaymentStatus.Pending;
                            break;
                    }
                    break;
                case "nok":
                    result = PaymentStatus.Pending;
                    break;
                default:
                    break;
            }
            return result;
        }
    }
}

