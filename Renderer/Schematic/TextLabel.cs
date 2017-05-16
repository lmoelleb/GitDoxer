namespace KiCadDoxer.Renderer.Schematic
{
    internal class TextLabel : Text
    {
        public TextLabel(RenderContext renderContext) : base(renderContext)
        {
        }

        protected override string CssClass => "label";

        protected override string Stroke => "rgb(0,0,0)";

        protected override TextVerticalJustify VerticalJustify => TextVerticalJustify.Bottom;
    }
}