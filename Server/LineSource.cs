using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace KiCadDoxer
{
    public class LineSource : IDisposable
    {
        private TaskCompletionSource<string> etagCompletionSource = new TaskCompletionSource<string>();
        private int lineNumber;

        // TODO: Make a base LineSource and an HTTP specific subclass to allow reading from files etc
        // - just like the render settings do.
        // TOTO: Refeactor to read tokens one by one instead of being line based. This will allow
        //       awaiting reading next token and get me out of the token index hell I have
        private string peekedLine;

        private bool readerCreated = false;
        private Task<TextReader> readerTask;
        private HashSet<Uri> redirectSet = new HashSet<Uri>();
        private List<IDisposable> toDispose = new List<IDisposable>();
        private Uri uri;

        public LineSource(Uri uri)
        {
            this.uri = uri;
            try
            {
                readerTask = CreateReader(uri);
            }
            catch
            {
                foreach (var disposable in toDispose)
                {
                    disposable.Dispose();
                }

                throw;
            }
        }

        public LineSource(TextReader reader)
        {
            this.readerTask = Task.FromResult(reader);
        }

        public int CurrentLineNumber
        {
            get
            {
                return lineNumber;
            }
        }

        public Task<string> ETag
        {
            get
            {
                // Could check the reader is created first, normally it should be. But it is not a
                // strict requirement - you just end up waiting long if it isn't. :)
                return etagCompletionSource.Task;
            }
        }

        public string Path => uri.ToString();

        public async Task<TextReader> CreateReader(Uri uri)
        {
            if (readerCreated)
            {
                throw new NotSupportedException("The reader can't be created twice.");
            }

            readerCreated = true;
            try
            {
                HttpClient client = new HttpClient();
                toDispose.Add(client);

                var response = await client.GetAsync(uri);
                toDispose.Add(response);

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
                etagCompletionSource.TrySetResult(etag);

                Stream stream;
                try
                {
                    stream = await response.Content.ReadAsStreamAsync();
                    toDispose.Add(stream);
                }
                catch (ObjectDisposedException)
                {
                    // Sorry about this mess. OF COURSE this code needs to be refactored so I do not
                    // get an exception if the request terminates as soon as the header is known.
                    // Unfortuately I live in something called "the real world" where my time is
                    // extremely valuable, so if this is what makes it work, it will have to do
                    // - then I can fix it if I get so lucky that this is the biggest problem I have :)
                    throw new TaskCanceledException();
                }

                StreamReader sr = new StreamReader(stream);
                toDispose.Add(sr);

                return sr;
            }
            catch (Exception ex)
            {
                etagCompletionSource.TrySetException(ex);
                throw;
            }
        }

        public async Task<string> Peek()
        {
            if (peekedLine == null)
            {
                var reader = await readerTask;

                // At the end of the file, multiple peeks will give multiple ReadLineAsync... I can
                // live with that :)
                peekedLine = await reader.ReadLineAsync();
                if (peekedLine != null)
                {
                    lineNumber++;
                }
            }

            return peekedLine;
        }

        public async Task<string> Read()
        {
            string result = await Peek();
            peekedLine = null;
            return result;
        }

        public async Task<string> ReadNotEof()
        {
            string result = await Read();
            if (result == null)
            {
                throw new FormatException($"Unexpected End of File on line {lineNumber} in {uri}");
            }

            return result;
        }

        public async Task<Token[]> ReadTokens()
        {
            var line = await Read();

            if (line == null)
            {
                return null;
            }
            List<Token> result = new List<Token>();
            StringBuilder current = new StringBuilder();

            // No idea how kicad escapes ", a question for another day!
            bool inString = false;
            bool emitEmptyString = false;
            int charIndex = 0;
            foreach (var c in line)
            {
                charIndex++;
                switch (c)
                {
                    case '\"':
                        if (inString)
                        {
                            inString = false;
                        }
                        else
                        {
                            if (current.Length == 0)
                            {
                                inString = true;
                                emitEmptyString = true;
                            }
                        }
                        break;

                    case ' ':
                    case '\t':
                        if (inString)
                        {
                            current.Append(c);
                        }
                        else
                        {
                            if (current.Length > 0 || emitEmptyString)
                            {
                                result.Add(new Token(current.ToString(), lineNumber, charIndex, uri.ToString()));
                                current.Length = 0;
                                emitEmptyString = false;
                            }
                        }
                        break;

                    default:
                        current.Append(c);
                        break;
                }
            }
            if (current.Length > 0 || emitEmptyString)
            {
                result.Add(new Token(current.ToString(), lineNumber, charIndex, uri.ToString()));
            }

            return result.ToArray();
        }

        public async Task<Token[]> ReadTokensNotEof()
        {
            var result = await ReadTokens();
            if (result == null)
            {
                throw new FormatException($"Unexpected End of File on line {lineNumber} in {uri}");
            }

            return result;
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (!readerCreated)
                {
                    try
                    {
                        // Hmm, is there a way to ensure an exception has a valid stack trace without
                        // actually throwing it?
                        throw new ObjectDisposedException("LineSource", "The LineSource was disposed without creating a reader that can supply the ETag.");
                    }
                    catch (ObjectDisposedException ex)
                    {
                        etagCompletionSource.TrySetException(ex);
                    }
                }

                disposedValue = true;
                foreach (var disposable in toDispose)
                {
                    disposable.Dispose();
                }
            }
        }

        #endregion IDisposable Support
    }
}