using System.Web.Mvc;
using System.Web.Routing;
using Nop.Web.Framework.Mvc.Routes;

namespace Nop.Plugin.Payments.ZarinPal
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            //PDT
            routes.MapRoute("Plugin.Payments.ZarinPal.PDTHandler",
                 "Plugins/PaymentZarinPal/PDTHandler",
                 new { controller = "PaymentZarinPal", action = "PDTHandler" },
                 new[] { "Nop.Plugin.Payments.ZarinPal.Controllers" }
            );
            //Cancel
            routes.MapRoute("Plugin.Payments.ZarinPal.CancelOrder",
                 "Plugins/PaymentZarinPal/CancelOrder",
                 new { controller = "PaymentZarinPal", action = "CancelOrder" },
                 new[] { "Nop.Plugin.Payments.ZarinPal.Controllers" }
            );
        }
        public int Priority
        {
            get
            {
                return 0;
            }
        }
    }
}
