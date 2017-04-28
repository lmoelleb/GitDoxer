using System;

namespace KiCadDoxer.Renderer.Exceptions
{
    public class KiCadFileFormatException : Exception
    {
        internal KiCadFileFormatException(Token token, string message) : this(token.LineSource, token.LineNumber, token.CharacterNumber, message)
        {
        }

        internal KiCadFileFormatException(LineSource lineSource, int lineNumber, int characterNumber, string message)
            : base($"{message} Line# {lineNumber} Col# {characterNumber} {lineSource.Url}")
        {
        }
    }
}