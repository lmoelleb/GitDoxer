using KiCadDoxer.Renderer.Exceptions;
using System;
using System.Globalization;
using System.Linq;

namespace KiCadDoxer.Renderer
{
    public class Token
    {
        private static string[] validBooleanFalse = { "N", "0" };
        private static string[] validBooleanTrue = { "Y", "1" };
        private LineSource lineSource;
        private string token;

        public Token(string token, LineSource lineSource, int characterNumber)
        {
            this.token = token ?? string.Empty; // Might regret this one day... will deal with that... one day
            this.lineSource = lineSource;
            this.CharacterNumber = characterNumber;
            this.LineNumber = lineSource.CurrentLineNumber;
        }

        public int LineNumber { get; private set; }

        internal int CharacterNumber { get; private set; }

        internal LineSource LineSource => lineSource;

        public char this[int index]
        {
            get
            {
                return token[index];
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

            return t.ToString();
        }

        public bool ToBoolean()
        {
            if (validBooleanTrue.Contains(token))
            {
                return true;
            }

            if (validBooleanFalse.Contains(token))
            {
                return false;
            }

            throw new KiCadFileFormatException(this, $"Expected one of the values {validBooleanTrue.Union(validBooleanFalse)}. Got \"{ToString()}\".");
        }

        public char ToChar()
        {
            if (token.Length != 1)
            {
                throw new KiCadFileFormatException(this, $"Expected a single character. Got \"{ToString()}\".");
            }

            return token[0];
        }

        public double ToDouble()
        {
            double result;
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                throw new KiCadFileFormatException(this, $"Expected a floating point number. Got \"{ToString()}\".");
            }

            return result;
        }

        public T ToEnum<T>() where T : struct
        {
            T result;
            if (!Enum.TryParse(token, true, out result))
            {
                throw new KiCadFileFormatException(this, $"Expected one of the values {string.Join(", ", Enum.GetNames(typeof(T)))}. Got \"{ToString()}\".");
            }

            return result;
        }

        public T ToEnumOrDefault<T>(T defaultValue) where T : struct
        {
            T result;
            if (!Enum.TryParse(token, true, out result))
            {
                result = defaultValue;
            }

            return result;
        }

        public int ToInt()
        {
            int result;
            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                throw new KiCadFileFormatException(this, $"Expected an integer number. Got \"{ToString()}\".");
            }

            return result;
        }

        public string ToLowerInvariant()
        {
            return token.ToLowerInvariant();
        }

        public override string ToString()
        {
            return token;
        }
    }
}