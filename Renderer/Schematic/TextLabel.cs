namespace KiCadDoxer.Renderer.Schematic
{
    internal class TextLabel : Text
    {
        public TextLabel(RenderContext renderContext) : base(renderContext)
        {
        }

        public TextLabel(RenderContext renderContext, int x, int y, int angle, string text, TextSettings textSettings)
            : base(renderContext, x, y, angle, text, textSettings)
        {
        }

        protected override string CssClass => "label";

        protected override string Stroke => "rgb(0,0,0)";

        protected override TextVerticalJustify VerticalJustify => TextVerticalJustify.Bottom;
    }
}