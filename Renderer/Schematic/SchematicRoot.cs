using KiCadDoxer.Renderer.Exceptions;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer.Schematic
{
    internal class SchematicRoot : RenderItem
    {
        private SchematicRoot(RenderContext renderContext) : base(renderContext)
        {
        }

        public static async Task<SchematicRoot> Render(RenderContext renderContext)
        {
            var root = new SchematicRoot(renderContext);
            await root.Render();
            return root;
        }

        private async Task Render()
        {
            await LineSource.Read("Schematic");
            await LineSource.Read("File");
            await LineSource.Read("Version");
            var version = await LineSource.Read(typeof(int));

            if (version != "2")
            {
                throw new KiCadFileFormatException(version, "Only file version 2 is supported, got version " + version);
            }

            await LineSource.Read(TokenType.LineBreak);

            await Writer.WriteStartElementAsync("svg");
            await Writer.WriteInheritedAttributeStringAsync("stroke-linecap", "round");
            await Writer.WriteInheritedAttributeStringAsync("stroke-linejoin", "round");
            await Writer.WriteInheritedAttributeStringAsync("fill", "none");
            await Writer.WriteInheritedAttributeStringAsync("class", "kicad schematics");
            await Writer.WriteInheritedAttributeStringAsync("stroke-width", Settings.DefaultStrokeWidth);

            bool fileCompleted = false;
            while (!fileCompleted)
            {
                Token token;
                await LineSource.SkipEmptyLines();
                token = await LineSource.Read(TokenType.Atom);
                switch ((string)token)
                {
                    case "$Descr":
                        await Description.Render(RenderContext);
                        break;

                    case "$EndSCHEMATC":

                        // Really... Someone decided to save an 'I'.. REALLY.
                        fileCompleted = true;

                        // Could check nothing follows, but not sure KiCad cares... I don't :)
                        break;

                    case "Wire":
                        await Wire.Render(RenderContext);
                        break;
                }
            }

            await Writer.WriteEndElementAsync("svg");
        }
    }
}