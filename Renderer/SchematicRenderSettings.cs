using System;
using System.Collections.Generic;
using System.Text;

namespace KiCadDoxer.Renderer
{
    public class SchematicRenderSettings : RenderSettings
    {
        public virtual double DefaultStrokeWidth => 6;

        public virtual HiddenPinRenderMode HiddenPinRenderMode => HiddenPinRenderMode.Hide;

        public virtual bool ShowPinNumbers => true;

        public virtual ComponentFieldRenderMode ShowComponentField(int fieldIndex)
        {
            return ComponentFieldRenderMode.Default;
        }
    }
}
