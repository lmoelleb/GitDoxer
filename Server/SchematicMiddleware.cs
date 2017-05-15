using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace KiCadDoxer.Server
{
    public static class SchematicMiddlewareExtensions
    {
        public static IApplicationBuilder UseSchematicMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SchematicMiddleware>();
        }
    }

    public class SchematicMiddleware
    {
        // Must have constructor with this signature, otherwise exception at run time
        public SchematicMiddleware(RequestDelegate next)
        {
            // This is an HTTP Handler, so no need to store next - we are not continuing down that
            // rabbit hole
        }

        public Task Invoke(HttpContext context)
        {
            var renderContext = new HttpRenderContext(context);
            return renderContext.Render();

            //return new SchematicRenderer().HandleSchematic(renderContext);
        }
    }
}