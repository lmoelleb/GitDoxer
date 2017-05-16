namespace KiCadDoxer.Renderer.Schematic
{
    internal class TextNotes : Text
    {
        public TextNotes(RenderContext renderContext) : base(renderContext)
        {
        }

        protected override string CssClass => "notes";

        protected override string Stroke => "rgb(0,0,132)";

        protected override TextVerticalJustify VerticalJustify => TextVerticalJustify.Bottom;
    }
}