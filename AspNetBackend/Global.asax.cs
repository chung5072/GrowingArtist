using AspNetBackend.App_Start;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;

namespace AspNetBackend
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            UnityConfig.RegisterComponents();
            GlobalConfiguration.Configure(WebApiConfig.Register); // WebApiConfig.Register �޼��尡 ȣ��
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
        }
    }
}
