using System.Threading.Tasks;

namespace KiCadDoxer.Renderer.Schematic
{
    internal class SchematicRoot : RenderItem
    {
        public SchematicRoot(RenderContext renderContext) : base(renderContext)
        {
        }

        public async Task Render(LineSource lineSource)
        {
            await Writer.WriteStartElementAsync("svg");
            await Writer.WriteInheritedAttributeStringAsync("stroke-linecap", "round");
            await Writer.WriteInheritedAttributeStringAsync("stroke-linejoin", "round");
            await Writer.WriteInheritedAttributeStringAsync("fill", "none");
            await Writer.WriteInheritedAttributeStringAsync("class", "kicad schematics");
            await Writer.WriteInheritedAttributeStringAsync("stroke-width", Settings.DefaultStrokeWidth);

            // Should probably do something useful here :)

            await Writer.WriteEndElementAsync("svg");
        }
    }
}