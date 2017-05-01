using KiCadDoxer.Renderer;
using Microsoft.AspNetCore.Http;
using System;
using System.Globalization;
using System.Linq;

namespace KiCadDoxer.Server
{
    public class HttpSchematicRenderSettings : SchematicRenderSettings
    {
        private static readonly string[] boolTrueStrings = new[] { "y", "yes", "1", "t", "true" };

        private readonly HttpContext httpContext;

        public HttpSchematicRenderSettings(HttpContext httpContext)
        {
            this.httpContext = httpContext;
            var query = httpContext.Request.Query;
            this.AddClasses = boolTrueStrings.Contains(((string)query["classes"] ?? string.Empty).ToLowerInvariant());

            HiddenPinRenderMode hiddenPinsRenderMode;
            if (!Enum.TryParse(query["hiddenpins"], true, out hiddenPinsRenderMode))
            {
                HiddenPinRenderMode = HiddenPinRenderMode;
            }
            else
            {
                hiddenPinsRenderMode = base.HiddenPinRenderMode;
            }

            ShowPinNumbers = ((string)query["pinnumbers"] ?? string.Empty).ToLowerInvariant() != "hidden";

            PrettyPrint = boolTrueStrings.Contains(((string)query["prettyprint"] ?? string.Empty).ToLowerInvariant());
        }

        public override bool AddClasses { get; }

        // TODO: Expose it, but need to deal with a double value that might need a BadRequest
        //       response if it is wrong
        public override double DefaultStrokeWidth => base.DefaultStrokeWidth;

        public override HiddenPinRenderMode HiddenPinRenderMode { get; }

        public override bool PrettyPrint { get; }

        public override bool ShowPinNumbers { get; }

        public override ComponentFieldRenderMode ShowComponentField(int fieldIndex)
        {
            string queryParameterName = "componentfield" + fieldIndex.ToString(CultureInfo.InvariantCulture);
            ComponentFieldRenderMode result;
            if (!Enum.TryParse(httpContext.Request.Query[queryParameterName], true, out result))
            {
                result = ComponentFieldRenderMode.Default;
            }

            return result;
        }
    }
}