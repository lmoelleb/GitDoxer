using KiCadDoxer.Renderer;
using KiCadDoxer.Renderer.Exceptions;
using Microsoft.AspNetCore.Http;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KiCadDoxer.Server
{
    public class SchematicRenderSettingsHttpRequest : SchematicRenderSettings
    {
        private static readonly string[] boolTrueStrings = new[] { "y", "yes", "1", "t", "true" };
        private bool addClasses;
        private bool addXlinkToSheets;
        private HttpContext context;
        private HiddenPinRenderMode? hiddenPins;
        private bool prettyPrint;
        private bool showPinNumbers;
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

            // Allow adding SVG to the extension as some (github, I am looking at you) expects the
            // extension to match the file type... wierd expectation, but can't change it!
            if (uri.LocalPath.EndsWith(".sch.svg", StringComparison.OrdinalIgnoreCase))
            {
                var builder = new UriBuilder(uri);
                builder.Path = uri.LocalPath.Substring(0, uri.LocalPath.Length - 4);
                uri = builder.Uri;
            }

            HiddenPinRenderMode hiddenPinsRenderMode;
            if (!Enum.TryParse(request.Query["hiddenpins"], true, out hiddenPinsRenderMode))
            {
                hiddenPins = HiddenPinRenderMode;
            }

            showPinNumbers = ((string)request.Query["pinnumbers"] ?? string.Empty).ToLowerInvariant() != "hidden";

            addXlinkToSheets = boolTrueStrings.Contains(((string)request.Query["xlinksheets"] ?? string.Empty).ToLowerInvariant());

            prettyPrint = boolTrueStrings.Contains(((string)request.Query["prettyprint"] ?? string.Empty).ToLowerInvariant());

            addClasses = boolTrueStrings.Contains(((string)request.Query["classes"] ?? string.Empty).ToLowerInvariant());

            context.Response.ContentType = "image/svg+xml";

            // A default value that will hopefully be replaced by an etag value
            context.Response.Headers["Cache-Control"] = "max-age=30";
        }

        public override bool AddClasses => addClasses;

        public override bool AddXlinkToSheets => addXlinkToSheets;

        public override HiddenPinRenderMode HiddenPinRenderMode => hiddenPins ?? base.HiddenPinRenderMode;

        public override bool PrettyPrint => prettyPrint;

        public override bool ShowPinNumbers => showPinNumbers;

        public static bool CanHandleContext(HttpContext context)
        {
            try
            {
                string path = context.Request.Path;
                if (path.EndsWith(".sch", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".sch.svg", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                string urlString = context.Request.Query["url"];
                if (!string.IsNullOrWhiteSpace(urlString))
                {
                    Uri uri = new Uri(urlString);
                    if (uri.LocalPath.EndsWith(".sch", StringComparison.OrdinalIgnoreCase) || uri.LocalPath.EndsWith(".sch.svg", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch (UriFormatException)
            {
                // TODO: Send exception to intellitrace - and it would also be nice to see it logged
                //       if no other middleware takes the request!
            }

            return false;
        }

        public override Task<LineSource> CreateLibraryLineSource(string libraryName, CancellationToken cancellationToken)
        {
            string path = new UriBuilder(uri) { Query = string.Empty }.ToString();

            // There should be a slash in a URL... if not... oh well
            path = path.Substring(0, path.LastIndexOf('/') + 1) + libraryName + uri.Query;

            return Task.FromResult<LineSource>(new LineSourceHttp(new Uri(path), cancellationToken));
        }

        public override Task<LineSource> CreateLineSource(CancellationToken cancellationToken) => Task.FromResult<LineSource>(new LineSourceHttp(uri, cancellationToken));

        // It would probably be better to have a specific class instead of the generic TextWriter -
        // then the methods to deal with etags etc can be moved to it, as they are NOT SETTINGS!
        public override Task<TextWriter> CreateOutputWriter(CancellationToken cancellationToken)
        {
            return Task.FromResult((TextWriter)new StreamWriter(this.context.Response.Body, Encoding.UTF8));
        }

        public override string GetRequestETagHeaderValue()
        {
            return context.Request.Headers["If-None-Match"];
        }

        public override async Task<bool> HandleException(Exception ex)
        {
            if (ex is KiCadFileFormatException || ex is KiCadFileNotAvailableException)
            {
                // The message (but not callstack) for these exception can be displayed to the caller
                // - they contain no sensitive information

                if (context.Response.HasStarted)
                {
                    // Too late to change the return code. For now, write the error as a comment -
                    // better than nothing. Also consider rendering it as actual text visible in the
                    // image (need to know if the root node has been written or not), and need access
                    // to the xml writer
                    try
                    {
                        SvgWriter writer = SvgWriter.Current;
                        if (writer.IsRootElementWritten && !writer.IsClosed)
                        {
                            await writer.WriteStartElementAsync("text");
                            await writer.WriteAttributeStringAsync("x", "0");
                            await writer.WriteAttributeStringAsync("y", "0");
                            await writer.WriteAttributeStringAsync("stroke", "rgb(255,0,0");
                            await writer.WriteAttributeStringAsync("fill", "rgb(255,0,0");
                            await writer.WriteAttributeStringAsync("font-size", "100");
                            await writer.WriteStringAsync(ex.Message);
                            await writer.WriteEndElementAsync("text");
                        }
                        else
                        {
                            // Most likely this will not be visible in any way, but it is the last
                            // chance to let the caller know something is wrong.
                            await writer.WriteCommentAsync(ex.Message);
                            await writer.FlushAsync();
                            return false; // Still throw the exception to let the entire pipeline know this went wrong!
                        }
                    }
                    catch
                    {
                        // TODO: Log this exception to Application Insights - it has no where else to
                        //       go as I want to rethrow the original exception.
                    }
                }
                else
                {
                    // The response has not yet started. So it is an option to return an actual error
                    // code. Consider using some middleware that handles this nicely - maybe on of
                    // them there fancy error pages :)
                    context.Response.ContentType = "text/plain; charset=\"UTF-8\"";
                    context.Response.StatusCode = 500;
                    if (ex is KiCadFileNotAvailableException)
                    {
                        context.Response.StatusCode = (int)((KiCadFileNotAvailableException)ex).StatusCode;
                    }
                    await context.Response.WriteAsync(ex.Message);

                    // In case the writer has started up, it will have stuff it will flush to the
                    // reponse stream For now accept this (write a newline to space it out a bit).
                    // Later stop it - worst case put a buffer inbetween where we can turn off
                    // further writes.
                    await context.Response.WriteAsync("\r\n\r\n");
                    return true;
                }
            }

            return false;
        }

        public override Task<bool> HandleMatchingETags(CancellationToken cancellationToken)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotModified;
            return Task.FromResult(true);
        }

        public override void SetResponseEtagHeaderValue(string etag)
        {
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["ETag"] = etag;
            base.SetResponseEtagHeaderValue(etag);
        }

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