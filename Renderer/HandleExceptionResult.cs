using System;
using System.Collections.Generic;
using System.Text;

namespace KiCadDoxer.Renderer
{
    [Flags]
    public enum HandleExceptionResult
    {
        Ignore = 0,
        Throw = 1,
        WriteToSvg = 2

    }
}
