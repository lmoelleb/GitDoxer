using KiCadDoxer.Renderer.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer
{
    public abstract class LineSource : IDisposable
    {
        private CancellationTokenSource combinedCancellationTokenSource;
        private CancellationTokenSource disposedCancellationTokenSource = new CancellationTokenSource();
        private Lazy<Task<string>> etagTask;
        private int lineNumber;

        private string peekedLine;

        private bool readerCreated = false;
        private Lazy<Task<TextReader>> readerTask;
        private List<IDisposable> toDispose = new List<IDisposable>();

        public LineSource(CancellationToken cancellationToken)
        {
            combinedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(disposedCancellationTokenSource.Token, cancellationToken);
            etagTask = new Lazy<Task<string>>(() => GetETag(combinedCancellationTokenSource.Token));
            readerTask = new Lazy<Task<TextReader>>(() => CreateReader(combinedCancellationTokenSource.Token));
            toDispose.Add(combinedCancellationTokenSource);
        }

        public int CurrentLineNumber
        {
            get
            {
                return lineNumber;
            }
        }

        public Task<string> ETag => etagTask.Value;

        public virtual string Url { get; internal set; }

        public async Task<string> Peek()
        {
            if (peekedLine == null)
            {
                var reader = await readerTask.Value;

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
                throw new KiCadFileFormatException(this, CurrentLineNumber, 0, $"Unexpected End of File");
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
                                result.Add(new Token(current.ToString(), this, charIndex));
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
                result.Add(new Token(current.ToString(), this, charIndex));
            }

            return result.ToArray();
        }

        public async Task<Token[]> ReadTokensNotEof()
        {
            var result = await ReadTokens();
            if (result == null)
            {
                throw new KiCadFileFormatException(this, CurrentLineNumber, 0, $"Unexpected End of File");
            }

            return result;
        }

        protected abstract Task<TextReader> CreateReader(CancellationToken cancellationToken);

        protected virtual Task<string> GetETag(CancellationToken cancellationToken)
        {
            return Task.FromResult<string>(null);
        }

        protected void RegisterForDisposal(IDisposable disposable)
        {
            toDispose.Add(disposable);
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
                disposedValue = true;
                disposedCancellationTokenSource.Cancel();

                foreach (var disposable in toDispose)
                {
                    disposable.Dispose();
                }
            }
        }

        #endregion IDisposable Support
    }
}