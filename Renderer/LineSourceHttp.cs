using KiCadDoxer.Renderer.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer
{
    public class LineSourceHttp : LineSource
    {
        private Lazy<Task<HttpResponseMessage>> responseMessageTask;
        private Uri url;
        private HttpClient client = new HttpClient();

        public LineSourceHttp(Uri url, CancellationToken cancellationToken)
            : base(cancellationToken)
        {
            this.url = url;
            responseMessageTask = new Lazy<Task<HttpResponseMessage>>(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new KiCadFileNotAvailableException(response.StatusCode, $"Got {(int)response.StatusCode} {GetStatusCodeMessage(response.StatusCode)} from: {url}");
                }

                response.EnsureSuccessStatusCode();
                RegisterForDisposal(response);
                return response;
            });
        }

        private static string GetStatusCodeMessage(HttpStatusCode code)
        {
            if (code == HttpStatusCode.OK)
            {
                return "OK";
            }

            // Must not name the builder "Bob"... must not... mu... oh screw it!
            StringBuilder bob = new StringBuilder();
            foreach (var c in code.ToString())
            {
                if (char.IsUpper(c) && bob.Length > 0)
                {
                    bob.Append(' ');
                }

                bob.Append(c);
            }

            return bob.ToString();
        }

        public override string Url => url.ToString();

        protected override async Task<TextReader> CreateReader(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await responseMessageTask.Value;
            cancellationToken.ThrowIfCancellationRequested();
            Stream stream = await response.Content.ReadAsStreamAsync();
            RegisterForDisposal(stream);
            var reader = new StreamReader(stream, Encoding.UTF8, true);
            RegisterForDisposal(reader);
            return reader;
        }

        protected override async Task<string> GetETag(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await responseMessageTask.Value;
            string etag = response.Headers.ETag?.Tag;
            if (string.IsNullOrEmpty(etag))
            {
                IEnumerable<string> lastModifiedValues;
                if (response.Headers.TryGetValues("Last-Modified", out lastModifiedValues))
                {
                    string lastModified = lastModifiedValues.FirstOrDefault();
                    if (!string.IsNullOrEmpty(lastModified))
                    {
                        etag = $"\"{lastModified}\"";
                    }
                }
            }

            return etag;
        }
    }
}