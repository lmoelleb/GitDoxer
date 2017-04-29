using KiCadDoxer.Renderer.Exceptions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer
{
    public abstract class LineSource : IDisposable
    {
        private const int ExpectedMaxTokenSize = 1024;
        private const int MaximumTokenSize = 1_000_000; // Max 1MB tokens to limit DOD attacks (not that I expect any) trying to exhaust memory
        private static bool[] isWhiteSpaceLookup;
        private int columnNumber;
        private CancellationTokenSource combinedCancellationTokenSource;
        private CancellationTokenSource disposedCancellationTokenSource = new CancellationTokenSource();
        private StringBuilder escapeStringBuilder = new StringBuilder();
        private Lazy<Task<string>> etagTask;
        private char lastChar;
        private int lineNumber = 1;
        private int? peekedChar;

        private string peekedLine;

        private Token peekedToken;

        private char[] readBuffer = new char[1024];

        private int readBufferLength = 0;

        private int readBufferPosition = 0;

        private bool readerCreated = false;

        private Lazy<Task<TextReader>> readerTask;

        private List<IDisposable> toDispose = new List<IDisposable>();

        private StringBuilder tokenStringBuilder = new StringBuilder();

        // Premature optimization - could be local to PeekToken, but this way it is only allocated
        // once :)
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

        internal TokenizerMode Mode { get; set; }

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

        public async Task<Token> ReadToken()
        {
            var result = await PeekToken();
            peekedToken = null;
            return result;
        }

        public async Task<Token> PeekToken()
        {
            if (Mode == TokenizerMode.Unspecified)
            {
                throw new InvalidOperationException("The Mode property must be set before reading a token.");
            }

            if (peekedToken != null)
            {
                return peekedToken;
            }

            tokenStringBuilder.Clear(); // Just in case :)
            escapeStringBuilder.Clear();

            bool inQuotedString = false;
            bool wasQuotedString = false;

            int tokenLineNumber = lineNumber;
            int tokenColumnNumber = columnNumber;

            while (true)
            {
                int read = await ReadChar();

                bool isWhiteSpace = IsWhiteSpace((char)read);

                // Skip leading whitespaces
                if (!inQuotedString && !wasQuotedString && tokenStringBuilder.Length == 0 && isWhiteSpace)
                {
                    continue;
                }

                bool isEoF = read == -1;
                bool isSExpressionToken = !inQuotedString && (read == '(' || read == ')');

                if (wasQuotedString && !(isEoF || isWhiteSpace || (isSExpressionToken && tokenStringBuilder.Length > 0)))
                {
                    throw new KiCadFileFormatException(this, tokenLineNumber, tokenColumnNumber, "Quoted text must be followed by a whitespace or a parenthesis");
                }


                if (isEoF || wasQuotedString || isWhiteSpace || (isSExpressionToken && tokenStringBuilder.Length > 0))
                {
                    // Put the whitespace or expression token back so we leave right after the token - Then method
                    // reading the next line knows it first has to read to the end of the current.
                    // That way we do not need to deal with the difference from whitespaces at the
                    // end of the line
                    peekedChar = read;
                    string tokenValue = tokenStringBuilder.ToString();

                    return new Token(tokenValue, this, tokenLineNumber, tokenColumnNumber);
                }

                if (isEoF)
                {
                    throw new KiCadFileFormatException(this, lineNumber, columnNumber, "Unexpected End of File.");
                }


                char c = (char)read;

                bool wasEscaped = false;
                if (inQuotedString)
                {
                    bool characterAvailable;
                    bool wasConsumed;
                    char escaped;
                    (characterAvailable, escaped, wasEscaped, wasConsumed) = DecodeEscape(c);

                    if (!wasConsumed)
                    {
                        peekedChar = c;
                    }

                    if (wasEscaped)
                    {
                        c = escaped;
                    }

                    if (!characterAvailable)
                    {
                        continue;
                    }

                    if (c == '\"' && !wasEscaped)
                    {
                        inQuotedString = false;
                        wasQuotedString = true;
                    }
                    else
                    {
                        tokenStringBuilder.Append(c);
                    }
                }
                else
                {
                    if (tokenStringBuilder.Length == 0 && c == '\"')
                    {
                        inQuotedString = true;
                    }
                    else if (Mode == TokenizerMode.SExpresionKiCad && c == '(')
                    {
                        return new Token(TokenType.ExpressionOpen, this, tokenLineNumber, tokenColumnNumber);
                    }
                    else if (Mode == TokenizerMode.SExpresionKiCad && c == ')')
                    {
                        return new Token(TokenType.ExpressionClose, this, tokenLineNumber, tokenColumnNumber);
                    }
                    else
                    {
                        tokenStringBuilder.Append(c);
                    }
                }
            }
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
                                result.Add(new Token(current.ToString(), this, lineNumber, charIndex));
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
                result.Add(new Token(current.ToString(), this, lineNumber, charIndex));
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

        private (bool CharacterAvailable, char Character, bool WasEscaped, bool WasConsumed) DecodeEscape(char c)
        {
            bool wasEscaped = true;
            int character = -1;
            bool wasConsumed = true;
            if (escapeStringBuilder.Length == 0)
            {
                if (c == '\\')
                {
                    escapeStringBuilder.Append(c);
                }
                else
                {
                    character = c;
                    wasEscaped = false;
                }
            }
            else if (escapeStringBuilder.Length == 1)
            {
                // Only the \ encountered
                switch (c)
                {
                    case 'a':
                        character = '\a';
                        break;

                    case 'b':
                        character = '\b';
                        break;

                    case 'f':
                        character = '\f';
                        break;

                    case 'n':
                        character = '\n';
                        break;

                    case 'r':
                        character = '\r';
                        break;

                    case 't':
                        character = '\t';
                        break;

                    case 'v':
                        character = '\v';
                        break;

                    case '\\':
                        character = '\\';
                        break;

                    case '\'':
                        character = '\'';
                        break;

                    case '\"':
                        character = '\"';
                        break;

                    case '?':
                        character = '?';
                        break;

                    case 'x':
                    case 'U':
                    case 'u':
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':

                        // Multicharacter escape
                        escapeStringBuilder.Append(c);
                        break;

                    default:
                        escapeStringBuilder.Append(c);
                        throw new KiCadFileFormatException(this, lineNumber, columnNumber, "Unsupported escape sequence: " + escapeStringBuilder.ToString());
                }
                if (character >= 0)
                {
                    escapeStringBuilder.Clear();
                }
            }
            else
            {
                // \x encoding is strange - it does not have a fixed length. Which looks somewhat
                // dogy to me, as I can't see how it knows when it ends if followed by a digit?
                bool expectHex = true;
                int minimumLengthWithPrefix;
                int maximumLengthWithPrefix;
                switch (escapeStringBuilder[1])
                {
                    case 'x':
                        minimumLengthWithPrefix = 3;
                        maximumLengthWithPrefix = 4; // TODO: Check if KiCad allow longer hex escapes
                        break;

                    case 'U':
                        minimumLengthWithPrefix = 10;
                        maximumLengthWithPrefix = 10;
                        break;

                    case 'u':
                        minimumLengthWithPrefix = 6;
                        maximumLengthWithPrefix = 6;
                        break;

                    default:

                        // Octal
                        minimumLengthWithPrefix = 2;
                        maximumLengthWithPrefix = 4;
                        expectHex = false;
                        break;
                }

                bool isValid = (c >= '0' && c <= '7') || (expectHex && ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f')));
                if (!isValid)
                {
                    if (escapeStringBuilder.Length >= minimumLengthWithPrefix)
                    {
                        wasConsumed = false;
                    }
                    else
                    {
                        escapeStringBuilder.Append(c);
                        throw new KiCadFileFormatException(this, lineNumber, columnNumber, "Unsupported escape sequence: " + escapeStringBuilder.ToString());
                    }
                }
                else
                {
                    escapeStringBuilder.Append(c);
                }

                if (!wasConsumed || escapeStringBuilder.Length >= maximumLengthWithPrefix)
                {
                    if (!expectHex)
                    {
                        int decode = 0;
                        for (int i = 1; i < escapeStringBuilder.Length; i++)
                        {
                            decode *= 8;
                            decode += escapeStringBuilder[i] - '0';
                        }

                        escapeStringBuilder.Clear();
                        character = (char)decode;
                    }
                    else
                    {
                        uint decode = uint.Parse(escapeStringBuilder.ToString(2, escapeStringBuilder.Length - 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

                        bool needsSurregate = escapeStringBuilder[1] == 'U' && decode > 0x10000;
                        escapeStringBuilder.Clear();

                        if (needsSurregate)
                        {
                            // Yikes - surregates!
                            decode -= 0x10000;
                            uint high = 0xD800 + (decode >> 10);
                            uint low = 0xDC00 + (decode & 0x03FF);

                            // Write the trailing surregate into the escape buffer as a 4 byte
                            // unicode char)
                            escapeStringBuilder.Append("\\u");
                            escapeStringBuilder.Append(low.ToString("X4", CultureInfo.InvariantCulture));
                            character = (char)high;
                        }
                        else
                        {
                            character = (char)decode;
                        }
                    }
                }
            }

            return (character >= 0, (char)character, wasEscaped, wasConsumed);
        }

        private bool IsWhiteSpace(char c)
        {
            // Characters from
            // https://github.com/KiCad/kicad-source-mirror/blob/master/common/dsnlexer.cpp isSpace
            if (isWhiteSpaceLookup == null)
            {
                // Could have a race condition, but all that would happen is multiple arrays are
                // allocated, and over time only one will be left in scope andther others discarded.
                // But initialize BEFORE setting, or sme ppor thread might end up with an empty list :)
                var temp = new bool[' ' + 1];
                temp[' '] = true;
                temp['\r'] = true;
                temp['\t'] = true;
                temp['\0'] = true;

                // The original does not terat \n as a whitespace. Yes dear KiCad developers, it is annoying
                // we do not have ocnsistent whitespaces, but DEAL WITH IT!
                temp['\n'] = true;
                isWhiteSpaceLookup = temp;
            }

            return c <= ' ' && isWhiteSpaceLookup[c];
        }

        private async Task<int> ReadChar()
        {
            if (peekedChar.HasValue)
            {
                int result = peekedChar.Value;
                if (result != -1)
                {
                    peekedChar = null;
                }
                return result;
            }

            if (readBufferLength == readBufferPosition)
            {
                var reader = await readerTask.Value;
                readBufferPosition = 0;
                readBufferLength = await reader.ReadAsync(readBuffer, 0, readBuffer.Length);

                if (readBufferLength == 0)
                {
                    peekedChar = -1;
                    return -1;
                }
            }

            var c = readBuffer[readBufferPosition++];
            if (c == '\n' && lastChar == '\r')
            {
                // The newline was processed for \r already!
                return c;
            }

            if (c == '\n' || c == '\r')
            {
                lineNumber++;
                columnNumber = 0;
            }
            else
            {
                columnNumber++;
            }

            return c;
        }

        private void UpdateLineAndColumnNumbers(char c)
        {
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