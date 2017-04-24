using Microsoft.AspNetCore.Http;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KiCadDoxer
{
    public class SchematicRenderSettingsHttpRequest : SchematicRenderSettings
    {
        private static readonly string[] boolTrueStrings = new[] { "y", "yes", "1", "t", "true" };
        private HttpContext context;
        private Uri uri;
        public SchematicRenderSettingsHttpRequest(HttpContext context)
        {
            this.context = context;
            var request = context.Request;

            // TODO: Proper error if no uri is specified.... or if it is invalid.
            string path = context.Request.Path;

            if (path.StartsWith("/github/"))
            {
                // TODO: Could remove the query string parameters consumed by KiCadDoxer
                // TODO: Only remove blob if it is in the correct location... do not want someone to
                //       loose a folder named "blob" :)
                uri = new Uri("https://raw.githubusercontent.com/" + path.Substring(8).Replace("/blob/", "/") + request.QueryString);
            }
            else
            {
                uri = new Uri(request.Query["url"]);
            }

            string scheme = uri.Scheme.ToLowerInvariant();
            // Stop attempts at file:// - I hope!
            if (scheme != "http" && scheme != "https")
            {
                throw new Exception("TODO: Return bad request response instead");
            }

            HiddenPinRenderMode hiddenPins;
            if (Enum.TryParse(request.Query["hiddenpins"], true, out hiddenPins))
            {
                base.HiddenPinRenderMode = hiddenPins;
            }

            base.ShowPinNumbers = ((string)request.Query["pinnumbers"] ?? string.Empty).ToLowerInvariant() != "hidden";

            base.AddXlinkToSheets = boolTrueStrings.Contains(((string)request.Query["xlinksheets"] ?? string.Empty).ToLowerInvariant());

            base.PrettyPrint = boolTrueStrings.Contains(((string)request.Query["prettyprint"] ?? string.Empty).ToLowerInvariant());

            base.AddClasses = boolTrueStrings.Contains(((string)request.Query["classes"] ?? string.Empty).ToLowerInvariant());

            context.Response.ContentType = "image/svg+xml";
        }

        public static bool CanHandleContext(HttpContext context)
        {
            try
            {
                string path = context.Request.Path;
                if (path.EndsWith(".sch", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                string uriString = context.Request.Query["url"];
                if (!string.IsNullOrWhiteSpace(uriString))
                {
                    Uri uri = new Uri(uriString);
                    if (uri.LocalPath.EndsWith(".sch", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch (UriFormatException)
            {
                // TODO: Send exception to intellitrace - and it would also be nice to see it logged if no other middleware takes the request!
            }

            return false;
        }



        public override Task<LineSource> CreateLibraryLineSource(string libraryName)
        {
            string path = new UriBuilder(uri) { Query = string.Empty }.ToString();

            // There should be a slash in a URL... if not... oh well
            path = path.Substring(0, path.LastIndexOf('/') + 1) + libraryName + uri.Query;

            return Task.FromResult(new LineSource(new Uri(path)));
        }

        public override Task<LineSource> CreateLineSource() => Task.FromResult(new LineSource(uri));

        public override Task<TextWriter> CreateOutputWriter() => Task.FromResult((TextWriter)new StreamWriter(this.context.Response.Body, Encoding.UTF8));

        public override ComponentFieldRenderMode ShowComponentField(int fieldIndex)
        {
            string queryParameterName = "componentfield" + fieldIndex.ToString(CultureInfo.InvariantCulture);
            ComponentFieldRenderMode result;
            if (!Enum.TryParse(context.Request.Query[queryParameterName], true, out result))
            {
                result = ComponentFieldRenderMode.Default;
            }

            return result;
        }
    }
}