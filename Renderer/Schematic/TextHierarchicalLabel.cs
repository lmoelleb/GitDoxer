using System;

namespace KiCadDoxer.Renderer.Schematic
{
    internal class TextHierarchicalLabel : TextWithShape
    {
        public TextHierarchicalLabel(RenderContext renderContext) : base(renderContext)
        {
        }

        protected override string CssClass => "hlabel";

        protected override bool IsTextHorizontalAligmentInversed => true;

        protected override string Stroke => "rgb(132,132,0)";

        protected override (int dx, int dy) UnrotatedTextOffset
        {
            get
            {
                double width = TextSettings.StrokeWidth;
                double ii = TextSettings.Size + TextMargin + width;
                return ((int)Math.Round(-ii), 0);
            }
        }
    }
}