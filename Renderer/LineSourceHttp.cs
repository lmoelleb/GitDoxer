using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer
{
    public class LineSourceHttp : LineSource
    {
        private Lazy<Task<HttpResponseMessage>> responseMessageTask;
        private Uri uri;
        private HttpClient client = new HttpClient();

        public LineSourceHttp(Uri uri, CancellationToken cancellationToken)
            : base(cancellationToken)
        {
            this.uri = uri;
            responseMessageTask = new Lazy<Task<HttpResponseMessage>>(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                RegisterForDisposal(response);
                return response;
            });
        }

        public override string Url => uri.ToString();

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