using KiCadDoxer.Renderer.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer
{
    public abstract class LineSource : IDisposable
    {
        private const int ExpectedMaximumTokenOrWhitespaceSize = 2048;
        private const int MaximumTokenSize = 1_000_000; // Max 1MB tokens to limit DOD attacks (not that I expect any) trying to exhaust memory
        private const int MaximumWhiteSpaceSize = 1_000_000;
        private static bool[] isWhiteSpaceLookup;
        private int columnNumber;
        private CancellationTokenSource combinedCancellationTokenSource;
        private CancellationTokenSource disposedCancellationTokenSource = new CancellationTokenSource();
        private Lazy<Task<string>> etagTask;
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
        private StringBuilder tokenStringBuilder = new StringBuilder(ExpectedMaximumTokenOrWhitespaceSize);
        private StringBuilder whiteSpaceStringBuilder = new StringBuilder(ExpectedMaximumTokenOrWhitespaceSize);

        public LineSource(CancellationToken cancellationToken)
        {
            combinedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(disposedCancellationTokenSource.Token, cancellationToken);
            etagTask = new Lazy<Task<string>>(() => GetETag(combinedCancellationTokenSource.Token));
            readerTask = new Lazy<Task<TextReader>>(() => CreateReader(combinedCancellationTokenSource.Token));
            toDispose.Add(combinedCancellationTokenSource);
        }

        public Task<string> ETag => etagTask.Value;

        public int LineNumber
        {
            get
            {
                return lineNumber;
            }
        }

        public virtual string Url { get; internal set; }

        internal TokenizerMode Mode { get; set; }

        public async Task SkipEmptyLines()
        {
            while ((await Peek()).Type == TokenType.LineBreak)
            {
                await Read();
            }
        }

        public async Task SkipUntilAfterLineBreak()
        {
            await SkipWhileNot(TokenType.LineBreak, TokenType.EndOfFile);
            await Read(); // Consume the linebreak (or EOF, but that is fine, EOF keeps coming :)
        }

        internal async Task<Token> Peek(params TokenTypeOrText[] typesOrTexts)
        {
            if (Mode == TokenizerMode.Unspecified)
            {
                throw new InvalidOperationException("The Mode property must be set before reading a token.");
            }

            if (peekedToken != null)
            {
                TokenTypeOrText.EnsureMatching(peekedToken, typesOrTexts);
                return peekedToken;
            }

            tokenStringBuilder.Clear();
            tokenStringBuilder.Capacity = Math.Min(tokenStringBuilder.Capacity, ExpectedMaximumTokenOrWhitespaceSize);
            whiteSpaceStringBuilder.Clear();
            whiteSpaceStringBuilder.Capacity = Math.Min(whiteSpaceStringBuilder.Capacity, ExpectedMaximumTokenOrWhitespaceSize);

            bool inQuotedString = false;
            bool wasQuotedString = false;
            bool lastWasEscapeStart = false;

            int tokenLineNumber = lineNumber;
            int tokenColumnNumber = columnNumber;

            while (true)
            {
                int read = await ReadChar();

                combinedCancellationTokenSource.Token.ThrowIfCancellationRequested();

                bool isWhiteSpace = IsWhiteSpace((char)read) && !inQuotedString;

                // Skip leading whitespaces - consider introducing a whitespace token instead of
                // having a separate section for whitespaces.
                if (!inQuotedString && !wasQuotedString && tokenStringBuilder.Length == 0 && isWhiteSpace)
                {
                    whiteSpaceStringBuilder.Append((char)read);
                    continue;
                }

                bool isEoF = read < 0;
                bool isNewLine = read == '\n';
                bool isSExpressionToken = Mode == TokenizerMode.SExpresionKiCad && !inQuotedString && (read == '(' || read == ')');

                if (wasQuotedString && !(isEoF || isWhiteSpace || isNewLine || isSExpressionToken))
                {
                    string message = Mode == TokenizerMode.SExpresionKiCad ?
                        "Quoted text must be followed by a whitespace." :
                        "Quoted text must be followed by a whitespace or a parenthesis.";
                    throw new KiCadFileFormatException(this, tokenLineNumber, tokenColumnNumber, message);
                }

                if ((isEoF || isWhiteSpace || isNewLine || isSExpressionToken) && tokenStringBuilder.Length > 0)
                {
                    // The current character terminates the token we have been building. So put the
                    // character back (set it in peeked, a bit simplistic I know) and return the
                    // token we already have-
                    peekedChar = read;
                    string escapedTokenValue = tokenStringBuilder.ToString();

                    peekedToken = new Token(whiteSpaceStringBuilder.ToString(), escapedTokenValue, this, tokenLineNumber, tokenColumnNumber);
                    TokenTypeOrText.EnsureMatching(peekedToken, typesOrTexts);
                    return peekedToken;
                }

                // We have now dealt with the token already being build up - what we have now is for
                // the current token Deal with the simple single character cases first

                if (isEoF)
                {
                    peekedToken = new Token(whiteSpaceStringBuilder.ToString(), string.Empty, TokenType.EndOfFile, this, tokenLineNumber, tokenColumnNumber);
                    TokenTypeOrText.EnsureMatching(peekedToken, typesOrTexts);
                    return peekedToken;
                }

                char c = (char)read;
                tokenStringBuilder.Append(c);

                if (c == '\n')
                {
                    // Special handling - steal any \r from the whitespace before - that way \r\n is
                    // seen as a single token
                    if (whiteSpaceStringBuilder.Length > 0 && whiteSpaceStringBuilder[whiteSpaceStringBuilder.Length - 1] == '\r')
                    {
                        tokenStringBuilder.Insert(0, '\r');
                        whiteSpaceStringBuilder.Remove(whiteSpaceStringBuilder.Length - 1, 1);
                    }

                    peekedToken = new Token(whiteSpaceStringBuilder.ToString(), tokenStringBuilder.ToString(), TokenType.LineBreak, this, tokenLineNumber, tokenColumnNumber);
                    TokenTypeOrText.EnsureMatching(peekedToken, typesOrTexts);
                    return peekedToken;
                }

                if (isSExpressionToken)
                {
                    TokenType type;
                    switch (c)
                    {
                        case '(':
                            type = TokenType.ExpressionOpen;
                            break;

                        case ')':
                            type = TokenType.ExpressionClose;
                            break;

                        default:
                            throw new NotImplementedException("Token not implemented: " + c);
                    }

                    peekedToken = new Token(whiteSpaceStringBuilder.ToString(), tokenStringBuilder.ToString(), type, this, tokenLineNumber, tokenColumnNumber);
                    TokenTypeOrText.EnsureMatching(peekedToken, typesOrTexts);
                    return peekedToken;
                }

                // Not a single character thing (well, could be, but we do not know right away), so
                // build it up

                if (!inQuotedString)
                {
                    if (c == '\"' && tokenStringBuilder.Length == 1)
                    {
                        // The token starts with a quote, so kick it into quoted string mode.
                        inQuotedString = true;
                    }
                }
                else
                {
                    // We only care enough about escapes to determine if a " terminates the string,
                    // the rest of escape handling is in the Token
                    if (!lastWasEscapeStart && c == '\"')
                    {
                        inQuotedString = false;
                        wasQuotedString = true;
                    }

                    if (lastWasEscapeStart)
                    {
                        lastWasEscapeStart = false;
                    }
                    else
                    {
                        lastWasEscapeStart = c == '\\';
                    }
                }
            }
        }

        internal async Task<Token> Read(params TokenTypeOrText[] typesOrTexts)
        {
            var result = await Peek(typesOrTexts);
            if (result.Type != TokenType.EndOfFile)
            {
                // Keep returning EndOfFile to avoid dealing with null
                peekedToken = null;
            }
            return result;
        }

        internal Task<IEnumerable<Token>> ReadAllTokensUntilEndOfLine()
        {
            // TODO: Clean up messy code I probably want to get rid of this method though (no reading
            // in bulk), so try to remove it instead of cleaning it up
            TokenTypeOrText eof = TokenType.EndOfFile;
            return ReadAllTokensWhileNot(TokenType.LineBreak, new TokenTypeOrText[] { eof });
        }

        internal async Task<IEnumerable<Token>> ReadAllTokensWhileNot(TokenTypeOrText typeOrText, params TokenTypeOrText[] typesOrTexts)
        {
            List<Token> result = new List<Token>();
            Token token;
            while ((token = await TryReadUnless(typeOrText, typesOrTexts)) != null)
            {
                result.Add(token);
            }

            return result.AsReadOnly();
        }

        internal async Task ReadNext(TokenTypeOrText typeOrText, params TokenTypeOrText[] typesOrTexts)
        {
            await SkipWhileNot(typeOrText, typesOrTexts);
        }

        internal async Task<string> ReadTextWhileNot(TokenTypeOrText typeOrText, params TokenTypeOrText[] typesOrTexts)
        {
            Token token;
            StringBuilder result = new StringBuilder();
            var peekedStart = await Peek();
            while ((token = await TryReadUnless(typeOrText, typesOrTexts)) != null)
            {
                result.Append(token.PreceedingWhiteSpace);
                result.Append((string)token);
            }

            // We still might have whitespace up to the token we are looking for, include that as well
            Token nextToken = await Peek();
            result.Append(nextToken.PreceedingWhiteSpace);
            nextToken.PreceedingWhiteSpace = string.Empty; // Should maybe have a nicer way to stop it form being used twice... oh well

            return result.ToString();
        }

        internal async Task SkipWhileNot(TokenTypeOrText typeOrText, params TokenTypeOrText[] typesOrTexts)
        {
            while (true)
            {
                Token token = await Peek();
                if (TokenTypeOrText.IsMatching(token, typeOrText, typesOrTexts))
                {
                    return;
                }

                await Read();
            }
        }

        internal async Task<Token> TryPeekIf(TokenTypeOrText typeOrText, params TokenTypeOrText[] typesOrTexts)
        {
            var peek = await Peek();
            if (TokenTypeOrText.IsMatching(peek, typeOrText, typesOrTexts))
            {
                return peek;
            }

            return null;
        }

        internal async Task<Token> TryPeekUnless(TokenTypeOrText typeOrText, params TokenTypeOrText[] typesOrTexts)
        {
            var peek = await Peek();
            if (TokenTypeOrText.IsMatching(peek, typeOrText, typesOrTexts))
            {
                return null;
            }

            return peek;
        }

        internal async Task<Token> TryReadIf(TokenTypeOrText typeOrText, params TokenTypeOrText[] typesOrTexts)
        {
            if (await TryPeekIf(typeOrText, typesOrTexts) != null)
            {
                // No need to check the type again... I hope :)
                return await Read();
            }

            return null;
        }

        internal async Task<Token> TryReadUnless(TokenTypeOrText typeOrText, params TokenTypeOrText[] typesOrTexts)
        {
            if (await TryPeekUnless(typeOrText, typesOrTexts) != null)
            {
                // No need to check the type again... I hope :)
                return await Read();
            }

            return null;
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

            if (c == '\n')
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

        internal class TokenTypeOrText
        {
            public static readonly IEnumerable<TokenTypeOrText> EndOfLineTokenTypes = new TokenTypeOrText[] { TokenType.LineBreak, TokenType.EndOfFile };
            private string tokenText;
            private TokenType? tokenType;

            private TokenTypeOrText(string tokenText)
            {
                this.tokenType = TokenType.Atom;
                this.tokenText = tokenText;
            }

            private TokenTypeOrText(TokenType tokenType)
            {
                this.tokenType = tokenType;
            }

            public static void EnsureMatching(Token token, TokenTypeOrText tokenTypeOrText, IEnumerable<TokenTypeOrText> typeOrTexts)
            {
                EnsureMatching(token, tokenTypeOrText.AsEnumerable().Concat(typeOrTexts));
            }

            public static void EnsureMatching(Token token, IEnumerable<TokenTypeOrText> typeOrTexts)
            {
                if (IsMatching(token, typeOrTexts))
                {
                    return;
                }

                var expectedTexts = typeOrTexts.Where(t => t.tokenText != null).Select(t => t.tokenText).Distinct().ToList();
                var expectedTypes = typeOrTexts.Where(t => t.tokenText != null).Select(t => t.tokenType.ToString()).Distinct().ToList();

                // Expected a token with the one of the values xxx or one of the types xxx

                // This is not localizable - so any I18N will require this to be split into multiple strings
                string valueText = null;
                if (expectedTexts.Count == 1)
                {
                    valueText = "the value ";
                }
                if (expectedTexts.Count > 1)
                {
                    valueText = "one of the values ";
                }
                if (expectedTexts.Count > 0)
                {
                    valueText += "\"" + string.Join("\", \"", expectedTexts) + "\"";
                }

                string typeText = null;
                if (expectedTypes.Count == 1)
                {
                    typeText = "the type ";
                }
                if (expectedTypes.Count > 1)
                {
                    typeText = "one of the types ";
                }
                if (expectedTypes.Count > 0)
                {
                    typeText += string.Join(", ", expectedTexts);
                }
                string joinerText = null;
                if (!string.IsNullOrEmpty(joinerText) && !string.IsNullOrEmpty(typeText))
                {
                    joinerText = " or ";
                }

                throw new KiCadFileFormatException(token, $"Expected a token with {expectedTexts}{joinerText}{expectedTypes}. Got \"{token}\" with type {token.Type}.");
            }

            public static implicit operator TokenTypeOrText(string text)
            {
                return new TokenTypeOrText(text);
            }

            public static implicit operator TokenTypeOrText(TokenType type)
            {
                return new TokenTypeOrText(type);
            }

            public static bool IsMatching(Token token, TokenTypeOrText typeOrText, IEnumerable<TokenTypeOrText> typeOrTexts)
            {
                return IsMatching(token, typeOrText.AsEnumerable().Concat(typeOrTexts));
            }

            public static bool IsMatching(Token token, IEnumerable<TokenTypeOrText> typeOrTexts)
            {
                bool hadData = false; // Ensure single enumeration in typical cases (no parse error)
                bool result = typeOrTexts.Any(t =>
                {
                    hadData = true;
                    return t.IsMatch(token);
                });

                if (!hadData)
                {
                    // No restriction applied, anything goes!
                    return true;
                }

                return result;
            }

            public bool IsMatch(Token token)
            {
                if (tokenType.HasValue && token.Type != tokenType.Value)
                {
                    return false;
                }
                else if (tokenText != null && token != tokenText)
                {
                    return false;
                }

                return true;
            }

            public override string ToString()
            {
                string result = tokenType.ToString();
                if (tokenType == TokenType.Atom && tokenText != null)
                {
                    result += $"{{{tokenText}}}";
                }

                return result;
            }

            internal IEnumerable<TokenTypeOrText> AsEnumerable()
            {
                yield return this;
            }
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