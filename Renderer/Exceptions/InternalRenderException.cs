using System;

namespace KiCadDoxer.Renderer.Exceptions
{
    /// <summary>
    ///     An exception in parser or rendering engine indicating an error in the parser or renderer,
    ///     not in the input file. The exception details should not be revealed to untrusted callers.
    /// </summary>
    public class InternalRenderException : Exception
    {
        internal InternalRenderException(string message) : base(message)
        {
        }

        internal InternalRenderException(Token token, string message) : this(token.LineSource, token.LineNumber, token.ColumnNumber, message)
        {
        }

        internal InternalRenderException(LineSource lineSource, int lineNumber, int characterNumber, string message)
            : base($"{message} Line# {lineNumber} Col# {characterNumber} {lineSource.Url}")
        {
        }
    }
}