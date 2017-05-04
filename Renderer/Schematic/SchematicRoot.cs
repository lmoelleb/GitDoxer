using KiCadDoxer.Renderer.Exceptions;
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
            await lineSource.Read("Schematic");
            await lineSource.Read("File");
            await lineSource.Read("Version");
            var version = await lineSource.Read(TokenType.Atom);
            
            if (version != "2")
            {
                throw new KiCadFileFormatException(version, "Only file version 2 is supported, got version " + version);
            }

            await lineSource.Read(TokenType.LineBreak);

            await Writer.WriteStartElementAsync("svg");
            await Writer.WriteInheritedAttributeStringAsync("stroke-linecap", "round");
            await Writer.WriteInheritedAttributeStringAsync("stroke-linejoin", "round");
            await Writer.WriteInheritedAttributeStringAsync("fill", "none");
            await Writer.WriteInheritedAttributeStringAsync("class", "kicad schematics");
            await Writer.WriteInheritedAttributeStringAsync("stroke-width", Settings.DefaultStrokeWidth);

            // Should probably do something useful here :)

            // Let's see if this stays - probably more used when looping through lines.
            // Really... a typo in the keyword... REALLY.
            await lineSource.Read("$EndSCHEMATC");

            await Writer.WriteEndElementAsync("svg");
        }
    }
}