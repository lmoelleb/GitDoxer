using System;
using System.Collections.Generic;
using System.Text;

namespace KiCadDoxer.Renderer
{
    public abstract class RenderSettings
    {
        public virtual bool PrettyPrint => false;
        public virtual bool AddClasses => false;

    }
}
