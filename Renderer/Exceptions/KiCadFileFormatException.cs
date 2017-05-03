using System;

namespace KiCadDoxer.Renderer.Exceptions
{
    /// <summary>
    ///     An exception caused by an error in the KiCad file. The message - but not the stacktrace -
    ///     can be revieled to an untrusted caller.
    /// </summary>
    public class KiCadFileFormatException : Exception
    {
        internal KiCadFileFormatException(Token token, string message) : this(token.LineSource, token.LineNumber, token.ColumnNumber, message)
        {
        }

        internal KiCadFileFormatException(LineSource lineSource, int lineNumber, int characterNumber, string message)
            : base($"{message} Line# {lineNumber} Col# {characterNumber} {lineSource.Url}")
        {
        }
    }
}