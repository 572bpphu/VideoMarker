using System.Web.Http;
using Newtonsoft.Json.Serialization;

namespace VideoTimelineApp
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Enable attribute routing (required for [RoutePrefix] / [Route] on controllers)
            config.MapHttpAttributeRoutes();

            // Fallback conventional route
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            // Use camelCase JSON so JavaScript receives { startTime, endTime, ... }
            var json = config.Formatters.JsonFormatter;
            json.SerializerSettings.ContractResolver =
                new CamelCasePropertyNamesContractResolver();

            // Remove XML formatter – only serve JSON
            config.Formatters.Remove(config.Formatters.XmlFormatter);
        }
    }
}
