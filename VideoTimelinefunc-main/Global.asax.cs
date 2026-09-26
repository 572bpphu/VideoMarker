using System.Data.Entity;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;
using VideoTimelineApp.Data;

namespace VideoTimelineApp
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            // Web API must be registered BEFORE MVC routes
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            // Enforce modern TLS 1.2 security protocol for HTTPS connections (Cloudinary requirement)
            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11;

            // Automatically create database on LocalDB if it does not exist
            Database.SetInitializer(new CreateDatabaseIfNotExists<AppDbContext>());
        }
    }
}
