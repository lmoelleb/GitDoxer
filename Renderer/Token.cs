using KiCadDoxer.Renderer.Exceptions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KiCadDoxer.Renderer
{
    // TODO: Refactor so tokens are unescaped in here, not in the line source.

    public class Token
    {
        private static string[] validBooleanFalse = { "N", "0" };
        private static string[] validBooleanTrue = { "Y", "1" };
        private string unescapedTokenValue;
        public string PreceedingWhiteSpace { get; internal set; }
        public string EscapedTokenValue { get; }

        internal Token(string preceedingWhiteSpace, string escapedTokenValue, TokenType type, LineSource lineSource, int lineNumber, int columnNumber) : this(preceedingWhiteSpace, escapedTokenValue, GetTextFromTokenType(type), lineSource, lineNumber, columnNumber)
        {
            Type = type;
        }

        internal Token(string preceedingWhiteSpace, string escapedTokenValue, string unescapedTokenValue, LineSource lineSource, int lineNumber, int columnNumber)
        {
            this.EscapedTokenValue = escapedTokenValue ?? string.Empty;
            this.unescapedTokenValue = unescapedTokenValue ?? string.Empty; // Might regret this one day... will deal with that... one day
            this.LineSource = lineSource;
            this.ColumnNumber = columnNumber;
            this.LineNumber = lineNumber;
            this.Type = TokenType.Atom;
            this.PreceedingWhiteSpace = preceedingWhiteSpace;
        }

        public int ColumnNumber { get; }

        public int LineNumber { get; }

        public TokenType Type { get; }

        internal LineSource LineSource { get; }

        public char this[int index]
        {
            get
            {
                return unescapedTokenValue[index];
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

            return t.unescapedTokenValue;
        }

        public bool ToBoolean()
        {
            if (validBooleanTrue.Contains(unescapedTokenValue))
            {
                return true;
            }

            if (validBooleanFalse.Contains(unescapedTokenValue))
            {
                return false;
            }

            throw new KiCadFileFormatException(this, $"Expected one of the values {validBooleanTrue.Union(validBooleanFalse)}. Got \"{ToString()}\".");
        }

        public char ToChar()
        {
            if (unescapedTokenValue.Length != 1)
            {
                throw new KiCadFileFormatException(this, $"Expected a single character. Got \"{ToString()}\".");
            }

            return unescapedTokenValue[0];
        }

        public double ToDouble()
        {
            double result;
            if (!double.TryParse(unescapedTokenValue, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                throw new KiCadFileFormatException(this, $"Expected a floating point number. Got \"{ToString()}\".");
            }

            return result;
        }

        public T ToEnum<T>() where T : struct
        {
            T result;
            if (!Enum.TryParse(unescapedTokenValue, true, out result))
            {
                throw new KiCadFileFormatException(this, $"Expected one of the values {string.Join(", ", Enum.GetNames(typeof(T)))}. Got \"{ToString()}\".");
            }

            return result;
        }

        public T ToEnumOrDefault<T>(T defaultValue) where T : struct
        {
            T result;
            if (!Enum.TryParse(unescapedTokenValue, true, out result))
            {
                result = defaultValue;
            }

            return result;
        }

        public int ToInt()
        {
            int result;
            if (!int.TryParse(unescapedTokenValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                throw new KiCadFileFormatException(this, $"Expected an integer number. Got \"{ToString()}\".");
            }

            return result;
        }

        public string ToLowerInvariant()
        {
            return unescapedTokenValue.ToLowerInvariant();
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
    }
}