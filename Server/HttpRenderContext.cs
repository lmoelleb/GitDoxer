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
    public class HttpRenderContext : RenderContext
    {
        private HttpContext httpContext;
        private Uri uri;

        public HttpRenderContext(HttpContext httpContext)
        {
            this.httpContext = httpContext;
            var request = httpContext.Request;

            // TODO: Proper error if no uri is specified.... or if it is invalid.
            string path = httpContext.Request.Path;

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

            httpContext.Response.ContentType = "image/svg+xml";

            // A default value that will hopefully be replaced by an etag value
            httpContext.Response.Headers["Cache-Control"] = "max-age=30";
        }

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
            return Task.FromResult((TextWriter)new StreamWriter(this.httpContext.Response.Body, Encoding.UTF8));
        }

        public override string GetRequestETagHeaderValue()
        {
            return httpContext.Request.Headers["If-None-Match"];
        }

        public override async Task<HandleExceptionResult> HandleException(bool canAttempSvgWrite, Exception ex)
        {
            bool isPublic = ex is KiCadFileFormatException || ex is KiCadFileNotAvailableException;
            string body = isPublic ? ex.Message : "";
            if (isPublic && canAttempSvgWrite && httpContext.Response.HasStarted)
            {
                return HandleExceptionResult.WriteToSvg;
            }
            else
            {
                // The response has not yet started. So it is an option to return an actual error
                // code. Consider using some middleware that handles this nicely - maybe on of
                // them there fancy error pages :)
                httpContext.Response.ContentType = "text/plain; charset=\"UTF-8\"";
                httpContext.Response.StatusCode = 500;
                if (ex is KiCadFileNotAvailableException)
                {
                    httpContext.Response.StatusCode = (int)((KiCadFileNotAvailableException)ex).StatusCode;
                }
                if (isPublic)
                {
                    await httpContext.Response.WriteAsync(ex.Message);
                }

                // In case the writer has started up, it will have stuff it will flush to the
                // reponse stream For now accept this (write a newline to space it out a bit).
                // Later stop it - worst case put a buffer inbetween where we can turn off
                // further writes.
                await httpContext.Response.WriteAsync("\r\n\r\n");

                return HandleExceptionResult.Ignore;
            }
        }

        public override Task<bool> HandleMatchingETags(CancellationToken cancellationToken)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.NotModified;
            return Task.FromResult(true);
        }

        public override void SetResponseEtagHeaderValue(string etag)
        {
            httpContext.Response.Headers["Cache-Control"] = "no-cache";
            httpContext.Response.Headers["ETag"] = etag;
            base.SetResponseEtagHeaderValue(etag);
        }

        protected override SchematicRenderSettings CreateSchematicRenderSettings()
        {
            return new HttpSchematicRenderSettings(httpContext);
        }

        public override CancellationToken CancellationToken => httpContext.RequestAborted;

    }
}