using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace KiCadDoxer
{
    public class LineSource : IDisposable
    {
        private int lineNumber;

        // TODO: Make a base LineSource and an HTTP specific subclass to allow reading from files etc
        // - just like the render settings do.
        // TOTO: Refeactor to read tokens one by one instead of being line based. This will allow
        //       awaiting reading next token and get me out of the token index hell I have
        private string peekedLine;

        private Task<TextReader> readerTask;
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

        public async Task<TextReader> CreateReader(Uri uri)
        {
            HttpClient client = new HttpClient();
            toDispose.Add(client);

            Stream stream = await client.GetStreamAsync(uri);
            toDispose.Add(stream);

            StreamReader sr = new StreamReader(stream);
            toDispose.Add(sr);

            return sr;
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

        public string Path => uri.ToString();

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