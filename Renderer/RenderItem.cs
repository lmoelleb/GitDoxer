namespace KiCadDoxer.Renderer
{
    internal class RenderItem
    {
        public RenderItem(RenderContext renderContext)
        {
            this.RenderContext = renderContext;
        }

        protected RenderContext RenderContext { get; private set; }

        protected SchematicRenderSettings Settings => RenderContext.SchematicRenderSettings;

        protected SvgWriter Writer => RenderContext.SvgWriter;
    }
}