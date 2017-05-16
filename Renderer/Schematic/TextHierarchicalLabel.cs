using System;

namespace KiCadDoxer.Renderer.Schematic
{
    internal class TextHierarchicalLabel : TextWithShape
    {
        public TextHierarchicalLabel(RenderContext renderContext) : base(renderContext)
        {
        }

        public TextHierarchicalLabel(RenderContext renderContext, int x, int y, int angle, string text, Shape shape, TextSettings textSettings)
            : base(renderContext, x, y, angle, text, shape, textSettings)
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
                if (TextSettings.HorizontalJustify == TextHorizontalJustify.Right)
                {
                    ii *= -1;
                }
                return ((int)Math.Round(ii), 0);
            }
        }
    }
}