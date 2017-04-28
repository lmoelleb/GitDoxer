using System;
using System.Net;

namespace KiCadDoxer.Renderer.Exceptions
{
    public class KiCadFileNotAvailableException : Exception
    {
        public KiCadFileNotAvailableException(HttpStatusCode statusCode, string message)
            : base(message)
        {
            this.StatusCode = statusCode;
        }

        public HttpStatusCode StatusCode { get; }
    }
}