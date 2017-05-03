using KiCadDoxer.Renderer.Exceptions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace KiCadDoxer.Renderer
{
    internal class Token
    {
        private static string[] validBooleanFalse = { "N", "0" };
        private static string[] validBooleanTrue = { "Y", "1" };
        private Lazy<string> unescapedToken;

        internal Token(string preceedingWhiteSpace, string escapedTokenValue, TokenType type, LineSource lineSource, int lineNumber, int columnNumber) : this(preceedingWhiteSpace, escapedTokenValue, lineSource, lineNumber, columnNumber)
        {
            Type = type;
        }

        internal Token(string preceedingWhiteSpace, string escapedTokenValue, LineSource lineSource, int lineNumber, int columnNumber)
        {
            this.EscapedTokenValue = escapedTokenValue ?? string.Empty;
            this.unescapedToken = new Lazy<string>(() => Unescape());
            this.LineSource = lineSource;
            this.ColumnNumber = columnNumber;
            this.LineNumber = lineNumber;
            this.Type = TokenType.Atom;
            this.PreceedingWhiteSpace = preceedingWhiteSpace;
        }

        public int ColumnNumber { get; }

        public string EscapedTokenValue { get; }

        public int LineNumber { get; }

        public string PreceedingWhiteSpace { get; internal set; }

        public TokenType Type { get; }

        internal LineSource LineSource { get; }

        public char this[int index]
        {
            get
            {
                return unescapedToken.Value[index];
            }
        }

        public static implicit operator bool(Token t)
        {
            return t.ToBoolean();
        }

        public static implicit operator char(Token t)
        {
            return t.ToChar();
        }

        public static implicit operator double(Token t)
        {
            return t.ToDouble();
        }

        public static implicit operator int(Token t)
        {
            return t.ToInt();
        }

        public static implicit operator string(Token t)
        {
            if (t == null)
            {
                return null;
            }

            return t.unescapedToken.Value;
        }

        public bool ToBoolean()
        {
            if (validBooleanTrue.Contains(unescapedToken.Value))
            {
                return true;
            }

            if (validBooleanFalse.Contains(unescapedToken.Value))
            {
                return false;
            }

            throw new KiCadFileFormatException(this, $"Expected one of the values {validBooleanTrue.Union(validBooleanFalse)}. Got \"{ToString()}\".");
        }

        public char ToChar()
        {
            if (unescapedToken.Value.Length != 1)
            {
                throw new KiCadFileFormatException(this, $"Expected a single character. Got \"{ToString()}\".");
            }

            return unescapedToken.Value[0];
        }

        public double ToDouble()
        {
            double result;
            if (!double.TryParse(unescapedToken.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                throw new KiCadFileFormatException(this, $"Expected a floating point number. Got \"{ToString()}\".");
            }

            return result;
        }

        public T ToEnum<T>() where T : struct
        {
            T result;
            if (!Enum.TryParse(unescapedToken.Value, true, out result))
            {
                throw new KiCadFileFormatException(this, $"Expected one of the values {string.Join(", ", Enum.GetNames(typeof(T)))}. Got \"{ToString()}\".");
            }

            return result;
        }

        public T ToEnumOrDefault<T>(T defaultValue) where T : struct
        {
            T result;
            if (!Enum.TryParse(unescapedToken.Value, true, out result))
            {
                result = defaultValue;
            }

            return result;
        }

        public int ToInt()
        {
            int result;
            if (!int.TryParse(unescapedToken.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                throw new KiCadFileFormatException(this, $"Expected an integer number. Got \"{ToString()}\".");
            }

            return result;
        }

        public string ToLowerInvariant()
        {
            return unescapedToken.Value.ToLowerInvariant();
        }

        public override string ToString()
        {
            return EscapedTokenValue;
        }

        private static string GetTextFromTokenType(TokenType type)
        {
            switch (type)
            {
                case TokenType.EndOfFile:
                    return "";

                case TokenType.ExpressionClose:
                    return ")";

                case TokenType.ExpressionOpen:
                    return "(";

                case TokenType.LineBreak:
                    return "\n";

                default:
                    throw new NotSupportedException($"Not supported for TokenType{type}");
            }
        }

        private IEnumerable<char> DecodeMultiCharacterEscapeCode(StringBuilder code, char nextChar)
        {
            // Set it up to decode octals, as this saves me from adding all digits 0-7 in the switch
            // statement :)
            int minimumLength = 1;
            int maximumLength = 3;
            bool isNextValid = nextChar >= '0' && nextChar <= '7';
            bool isOctal = true;

            switch (code[0])
            {
                case 'x':
                    isOctal = false;
                    maximumLength = 7; // No limit according to specs, but unicode says max 6 - so do 7 so the correct error handling kicks in later :)
                    break;

                case 'u':
                    isOctal = false;
                    maximumLength = 4;
                    minimumLength = 4;
                    break;

                case 'U':
                    isOctal = false;
                    maximumLength = 8;
                    minimumLength = 8;
                    break;
            }

            if (!isOctal)
            {
                isNextValid |= (nextChar >= 'A' && nextChar <= 'F') || (nextChar >= 'a' && nextChar <= 'f') || (nextChar >= '8' && nextChar <= '9');
            }

            int offset = isOctal ? 0 : 1;
            int codeLength = code.Length - offset;

            if (code.Length < minimumLength && !isNextValid)
            {
                throw new KiCadFileFormatException(this, $"Invalid escape sequence: \\{code.ToString() + nextChar}.");
            }

            if (codeLength == maximumLength || !isNextValid)
            {
                // We have reached the end station.
                int decoded = 0;
                for (int i = offset; i < code.Length; i++)
                {
                    char c = code[i];
                    decoded *= isOctal ? 8 : 16;
                    int cVal;
                    if (c <= '9') cVal = c - '0';
                    else if (c <= 'F') cVal = c - 'A' + 10;
                    else cVal = c - 'a' + 10;
                    decoded += cVal;
                    if (decoded > 0x10FFFF)
                    {
                        throw new KiCadFileFormatException(this, $"Invalid escape sequence: \\{code.ToString()}.");
                    }
                }

                if (decoded >= 0x10000)
                {
                    // Requires surregate
                    decoded -= 0x10000;
                    yield return (char)(0xD800 + (decoded >> 10));
                    yield return (char)(0xDC00 + (decoded & 0x03FF));
                }
                else
                {
                    yield return (char)decoded;
                }
            }
        }

        private IEnumerable<char> DecodeSingleCharacterEscapeCode(char c, char nextChar)
        {
            switch (c)
            {
                case 'a':
                    yield return '\a';
                    break;

                case 'b':
                    yield return '\b';
                    break;

                case 'f':
                    yield return '\f';
                    break;

                case 'n':
                    yield return '\n';
                    break;

                case 'r':
                    yield return '\r';
                    break;

                case 't':
                    yield return '\t';
                    break;

                case 'v':
                    yield return '\v';
                    break;

                case '\\':
                    yield return '\\';
                    break;

                case '\'':
                    yield return '\'';
                    break;

                case '\"':
                    yield return '\"';
                    break;

                case '?':
                    yield return '?';
                    break;

                case 'x':
                case 'U':
                case 'u':

                    // Multicharacter escape
                    break;

                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                    if (nextChar < '0' || nextChar > '7')
                    {
                        yield return (char)(c - '0');
                    }

                    // else multicharacter escape
                    break;

                default:
                    throw new KiCadFileFormatException(this, "Invalid escape sequence: \\" + c);
            }
        }

        private string Unescape()
        {
            var escapedTokenValue = EscapedTokenValue;

            if (string.IsNullOrEmpty(escapedTokenValue))
            {
                return "";
            }

            if (escapedTokenValue[0] != '\"')
            {
                // Not a quoted string
                return escapedTokenValue;
            }

            if (!escapedTokenValue.Contains('\\'))
            {
                // Quoted string but no escaping, so simply skip the quotes
                return escapedTokenValue.Substring(1, escapedTokenValue.Length - 2);
            }

            StringBuilder resultBuilder = new StringBuilder();
            StringBuilder escapeCode = new StringBuilder();

            bool inEscape = false;
            for (int i = 1; i < escapedTokenValue.Length - 1; i++)
            {
                // Skip the quotes at both ends - the lineSource tokenizer should make sure they are
                // always present
                char c = escapedTokenValue[i];

                if (inEscape)
                {
                    escapeCode.Append(c);
                    IEnumerable<char> characters;
                    if (escapeCode.Length == 1)
                    {
                        characters = DecodeSingleCharacterEscapeCode(c, escapedTokenValue[i + 1]);
                    }
                    else
                    {
                        characters = DecodeMultiCharacterEscapeCode(escapeCode, escapedTokenValue[i + 1]);
                    }

                    foreach (var escaped in characters)
                    {
                        resultBuilder.Append(escaped);

                        // Yes, might clear twice for surregates - not worth checking :)
                        inEscape = false;
                        escapeCode.Clear();
                    }
                }
                else
                {
                    if (c == '\\')
                    {
                        inEscape = true;
                    }
                    else
                    {
                        resultBuilder.Append(c);
                    }
                }
            }

            return resultBuilder.ToString();
        }
    }
}