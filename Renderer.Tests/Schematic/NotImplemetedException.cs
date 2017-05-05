using System;

namespace KiCadDoxer.Renderer.Tests.Schematic
{
    internal class NotImplemetedException : Exception
    {
        public NotImplemetedException()
        {
        }

        public NotImplemetedException(string message) : base(message)
        {
        }

        public NotImplemetedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}