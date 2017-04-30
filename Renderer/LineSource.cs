using KiCadDoxer.Renderer.Exceptions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer
{
    public abstract class LineSource : IDisposable
    {
        private const int MaximumTokenSize = 1_000_000; // Max 1MB tokens to limit DOD attacks (not that I expect any) trying to exhaust memory
        private const int MaximumWhiteSpaceSize = 1_000_000;
        private const int ExpectedMaximumTokenOrWhitespaceSize = 2048;
        private static bool[] isWhiteSpaceLookup;
        private int columnNumber;
        private CancellationTokenSource combinedCancellationTokenSource;
        private CancellationTokenSource disposedCancellationTokenSource = new CancellationTokenSource();
        private StringBuilder escapedCharacterBuilder = new StringBuilder();
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
        private StringBuilder unescapedTokenStringBuilder = new StringBuilder(ExpectedMaximumTokenOrWhitespaceSize);
        private StringBuilder whiteSpaceStringBuilder = new StringBuilder(ExpectedMaximumTokenOrWhitespaceSize);
        private StringBuilder escapedTokenStringBuilder = new StringBuilder(ExpectedMaximumTokenOrWhitespaceSize);

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

        public async Task<Token> TryPeekUnless(TokenTypeOrText typeOrText, params TokenTypeOrText[] typesOrTexts)
        {
            var peek = await Peek();
            if (TokenTypeOrText.IsMatching(peek, typeOrText, typesOrTexts))
            {
                return null;
            }

            return peek;
        }

        public async Task<Token> TryPeekIf(TokenTypeOrText typeOrText, params TokenTypeOrText[] typesOrTexts)
        {
            var peek = await Peek();
            if (TokenTypeOrText.IsMatching(peek, typeOrText, typesOrTexts))
            {
                return peek;
            }

            return null;
        }

        public async Task<Token> TryReadIf(TokenTypeOrText typeOrText, params TokenTypeOrText[] typesOrTexts)
        {
            if (await TryPeekIf(typeOrText, typesOrTexts) != null)
            {
                // No need to check the type again... I hope :)
                return await Read();
            }

            return null;
        }

        public async Task<Token> TryReadUnless(TokenTypeOrText typeOrText, params TokenTypeOrText[] typesOrTexts)
        {
            if (await TryPeekUnless(typeOrText, typesOrTexts) != null)
            {
                // No need to check the type again... I hope :)
                return await Read();
            }

            return null;
        }

         public async Task SkipWhileNot(TokenTypeOrText typeOrText, params TokenTypeOrText[] typesOrTexts)
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
        public async Task SkipUntilAfterLineBreak()
        {
            await SkipWhileNot(TokenType.LineBreak, TokenType.EndOfFile);
            await Read(); // Consume the linebreak (or EOF, but that is fine, EOF keeps coming :)
        }

        public async Task SkipEmptyLines()
        {
            while ((await Peek()).Type == TokenType.LineBreak)
            {
                await Read();
            }
        }

        public async Task ReadNext(TokenTypeOrText typeOrText, params TokenTypeOrText[] typesOrTexts)
        {
            await SkipWhileNot(typeOrText, typesOrTexts);
        }

        public async Task<string> ReadTextWhileNot(TokenTypeOrText typeOrText, params TokenTypeOrText[] typesOrTexts)
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

        public async Task<IEnumerable<Token>> ReadAllTokensWhileNot(TokenTypeOrText typeOrText, params TokenTypeOrText[] typesOrTexts)
        {
            List<Token> result = new List<Token>();
            Token token;
            while ((token = await TryReadUnless(typeOrText, typesOrTexts)) != null)
            {
                result.Add(token);
            }

            return result.AsReadOnly();
        }

        public Task<IEnumerable<Token>> ReadAllTokensUntilEndOfLine()
        {
            // TODO: Clean up messy code
            // I probably want to get rid of this method though (no reading in bulk), so try to remove it instead of cleaning it up
            TokenTypeOrText eof = TokenType.EndOfFile;
            return ReadAllTokensWhileNot(TokenType.LineBreak, new TokenTypeOrText[] { eof });
        }


        public async Task<Token> Peek(params TokenTypeOrText[] typesOrTexts)
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

            unescapedTokenStringBuilder.Clear();
            unescapedTokenStringBuilder.Capacity = Math.Min(unescapedTokenStringBuilder.Capacity, ExpectedMaximumTokenOrWhitespaceSize);
            escapedTokenStringBuilder.Clear();
            escapedTokenStringBuilder.Capacity = Math.Min(escapedTokenStringBuilder.Capacity, ExpectedMaximumTokenOrWhitespaceSize);
            escapedCharacterBuilder.Clear();
            whiteSpaceStringBuilder.Clear();
            whiteSpaceStringBuilder.Capacity = Math.Min(whiteSpaceStringBuilder.Capacity, ExpectedMaximumTokenOrWhitespaceSize);


            bool inQuotedString = false;
            bool wasQuotedString = false;

            int tokenLineNumber = lineNumber;
            int tokenColumnNumber = columnNumber;

            while (true)
            {
                int read = await ReadChar();

                bool isWhiteSpace = IsWhiteSpace((char)read) && !inQuotedString;

                // Skip leading whitespaces
                if (!inQuotedString && !wasQuotedString && unescapedTokenStringBuilder.Length == 0 && isWhiteSpace)
                {
                    whiteSpaceStringBuilder.Append((char)read);
                    continue;
                }

                bool isEoF = read < 0;
                bool isNewLine = read == '\n';
                bool isSExpressionToken = !inQuotedString && (read == '(' || read == ')');

                if (wasQuotedString && !(isEoF || isWhiteSpace || isNewLine || (isSExpressionToken && unescapedTokenStringBuilder.Length > 0)))
                {
                    throw new KiCadFileFormatException(this, tokenLineNumber, tokenColumnNumber, "Quoted text must be followed by a whitespace or a parenthesis");
                }

                if ((isEoF || wasQuotedString || isWhiteSpace || isNewLine || isSExpressionToken) && escapedTokenStringBuilder.Length > 0)
                {
                    // Put the whitespace or expression token back so we leave right after the token
                    // - Then method reading the next line knows it first has to read to the end of
                    // the current. That way we do not need to deal with the difference from
                    // whitespaces at the end of the line
                    peekedChar = read;
                    string unescapedTokenValue = unescapedTokenStringBuilder.ToString();
                    string escapedTokenValue = escapedTokenStringBuilder.ToString();

                    peekedToken = new Token(whiteSpaceStringBuilder.ToString(), escapedTokenValue, unescapedTokenValue, this, tokenLineNumber, tokenColumnNumber);
                    TokenTypeOrText.EnsureMatching(peekedToken, typesOrTexts);
                    return peekedToken;
                }

                if (isEoF)
                {
                    peekedToken = new Token(whiteSpaceStringBuilder.ToString(), escapedTokenStringBuilder.ToString(), TokenType.EndOfFile, this, tokenLineNumber, tokenColumnNumber);
                    TokenTypeOrText.EnsureMatching(peekedToken, typesOrTexts);
                    return peekedToken;
                }

                char c = (char)read;
                escapedTokenStringBuilder.Append(c);

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
                        escapedTokenStringBuilder.Remove(escapedTokenStringBuilder.Length - 1, 1);
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
                        if (unescapedTokenStringBuilder.Length == MaximumTokenSize)
                        {
                            throw new KiCadFileFormatException(this, tokenLineNumber, tokenColumnNumber, $"Maximum token length of {MaximumTokenSize} exceeded");
                        }

                        unescapedTokenStringBuilder.Append(c);
                    }
                }
                else
                {
                    if (unescapedTokenStringBuilder.Length == 0 && c == '\"')
                    {
                        inQuotedString = true;
                    }
                    else if (Mode == TokenizerMode.SExpresionKiCad && c == '(')
                    {
                        peekedToken = new Token(whiteSpaceStringBuilder.ToString(), escapedTokenStringBuilder.ToString(), TokenType.ExpressionOpen, this, tokenLineNumber, tokenColumnNumber);
                        TokenTypeOrText.EnsureMatching(peekedToken, typesOrTexts);
                        return peekedToken;
                    }
                    else if (Mode == TokenizerMode.SExpresionKiCad && c == ')')
                    {
                        peekedToken = new Token(whiteSpaceStringBuilder.ToString(), escapedTokenStringBuilder.ToString(), TokenType.ExpressionClose, this, tokenLineNumber, tokenColumnNumber);
                        TokenTypeOrText.EnsureMatching(peekedToken, typesOrTexts);
                        return peekedToken;
                    }
                    else if (c == '\n')
                    {
                        peekedToken = new Token(whiteSpaceStringBuilder.ToString(), escapedTokenStringBuilder.ToString(), TokenType.LineBreak, this, tokenLineNumber, tokenColumnNumber);
                        TokenTypeOrText.EnsureMatching(peekedToken, typesOrTexts);
                        return peekedToken;
                    }
                    else
                    {
                        if (unescapedTokenStringBuilder.Length == MaximumTokenSize)
                        {
                            throw new KiCadFileFormatException(this, tokenLineNumber, tokenColumnNumber, $"Maximum token length of {MaximumTokenSize} exceeded");
                        }

                        unescapedTokenStringBuilder.Append(c);
                    }
                }
            }
        }

        public async Task<Token> Read(params TokenTypeOrText[] typesOrTexts)
        {
            var result = await Peek(typesOrTexts);
            if (result.Type != TokenType.EndOfFile)
            {
                // Keep returning EndOfFile to avoid dealing with null 
                peekedToken = null;
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
            if (escapedCharacterBuilder.Length == 0)
            {
                if (c == '\\')
                {
                    escapedCharacterBuilder.Append(c);
                }
                else
                {
                    character = c;
                    wasEscaped = false;
                }
            }
            else if (escapedCharacterBuilder.Length == 1)
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
                        escapedCharacterBuilder.Append(c);
                        break;

                    default:
                        escapedCharacterBuilder.Append(c);
                        throw new KiCadFileFormatException(this, lineNumber, columnNumber, "Unsupported escape sequence: " + escapedCharacterBuilder.ToString());
                }
                if (character >= 0)
                {
                    escapedCharacterBuilder.Clear();
                }
            }
            else
            {
                // \x encoding is strange - it does not have a fixed length. Which looks somewhat
                // dogy to me, as I can't see how it knows when it ends if followed by a digit?
                bool expectHex = true;
                int minimumLengthWithPrefix;
                int maximumLengthWithPrefix;
                switch (escapedCharacterBuilder[1])
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
                    if (escapedCharacterBuilder.Length >= minimumLengthWithPrefix)
                    {
                        wasConsumed = false;
                    }
                    else
                    {
                        escapedCharacterBuilder.Append(c);
                        throw new KiCadFileFormatException(this, lineNumber, columnNumber, "Unsupported escape sequence: " + escapedCharacterBuilder.ToString());
                    }
                }
                else
                {
                    escapedCharacterBuilder.Append(c);
                }

                if (!wasConsumed || escapedCharacterBuilder.Length >= maximumLengthWithPrefix)
                {
                    if (!expectHex)
                    {
                        int decode = 0;
                        for (int i = 1; i < escapedCharacterBuilder.Length; i++)
                        {
                            decode *= 8;
                            decode += escapedCharacterBuilder[i] - '0';
                        }

                        escapedCharacterBuilder.Clear();
                        character = (char)decode;
                    }
                    else
                    {
                        uint decode = uint.Parse(escapedCharacterBuilder.ToString(2, escapedCharacterBuilder.Length - 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

                        bool needsSurregate = escapedCharacterBuilder[1] == 'U' && decode > 0x10000;
                        escapedCharacterBuilder.Clear();

                        if (needsSurregate)
                        {
                            // Yikes - surregates!
                            decode -= 0x10000;
                            uint high = 0xD800 + (decode >> 10);
                            uint low = 0xDC00 + (decode & 0x03FF);

                            // Write the trailing surregate into the escape buffer as a 4 byte
                            // unicode char)
                            escapedCharacterBuilder.Append("\\u");
                            escapedCharacterBuilder.Append(low.ToString("X4", CultureInfo.InvariantCulture));
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

        public class TokenTypeOrText
        {
            private string tokenText;
            private TokenType? tokenType;
            public static readonly IEnumerable<TokenTypeOrText> EndOfLineTokenTypes = new TokenTypeOrText[] { TokenType.LineBreak, TokenType.EndOfFile }; 
            

            private TokenTypeOrText(string tokenText)
            {
                this.tokenType = TokenType.Atom;
                this.tokenText = tokenText;
            }

            private TokenTypeOrText(TokenType tokenType)
            {
                this.tokenType = tokenType;
            }

            public static implicit operator TokenTypeOrText(string text)
            {
                return new TokenTypeOrText(text);
            }

            public static implicit operator TokenTypeOrText(TokenType type)
            {
                return new TokenTypeOrText(type);
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

            internal IEnumerable<TokenTypeOrText> AsEnumerable()
            {
                yield return this;
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

            public override string ToString()
            {
                string result = tokenType.ToString();
                if (tokenType == TokenType.Atom && tokenText != null)
                {
                    result += $"{{{tokenText}}}";
                }

                return result;
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