using System;
using System.Globalization;
using System.Linq;

namespace KiCadDoxer.Renderer
{
    public class Token
    {
        private static string[] validBooleanFalse = { "N", "0" };
        private static string[] validBooleanTrue = { "Y", "1" };
        private int characterNumber;
        private int lineNumber;
        private string sourceUri;
        private string token;

        public Token(string token, int lineNumber, int characterNumber, string sourceUri)
        {
            this.token = token ?? string.Empty; // Might regret this one day... will deal with that... one day
            this.lineNumber = lineNumber;
            this.characterNumber = characterNumber;
            this.sourceUri = sourceUri;
        }

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

            // TODO: It appears FileFormatException is not in .NET Core? - check indetail if I am
            //       just missing a reference and if not, implement custom exception
            throw new FormatException($"Expected one of the values {validBooleanTrue.Union(validBooleanFalse)}. Got \"{ToString()}\" at line# {lineNumber}, character# {characterNumber} in {sourceUri}.");
        }

        public char ToChar()
        {
            if (token.Length != 1)
            {
                throw new FormatException($"Expected a single character, got \"{ToString()}\" at line# {lineNumber}, character# {characterNumber} in {sourceUri}.");
            }

            return token[0];
        }

        public double ToDouble()
        {
            double result;
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                // TODO: It appears FileFormatException is not in .NET Core? - check indetail if I am
                //       just missing a reference and if not, implement custom exception
                throw new FormatException($"Expected a floating point number, got \"{ToString()}\" at line# {lineNumber}, character# {characterNumber} in {sourceUri}.");
            }

            return result;
        }

        public T ToEnum<T>() where T : struct
        {
            T result;
            if (!Enum.TryParse(token, true, out result))
            {
                // TODO: It appears FileFormatException is not in .NET Core? - check indetail if I am
                //       just missing a reference and if not, implement custom exception
                throw new FormatException($"Expected one of the values {string.Join(", ", Enum.GetNames(typeof(T)))}. Got \"{ToString()}\" at line# {lineNumber}, character# {characterNumber} in {sourceUri}.");
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
                // TODO: It appears FileFormatException is not in .NET Core? - check indetail if I am
                //       just missing a reference and if not, implement custom exception
                throw new FormatException($"Expected an integer number, got \"{ToString()}\" at line# {lineNumber}, character# {characterNumber} in {sourceUri}.");
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

        public string ToUpperInvariant()
        {
            return token.ToUpperInvariant();
        }
    }
}