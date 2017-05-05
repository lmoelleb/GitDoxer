using System.Threading.Tasks;

namespace KiCadDoxer.Renderer.Schematic
{
    internal class Wire : RenderItem
    {
        private Wire(RenderContext renderContext) : base(renderContext)
        {
        }

        public static async Task<Wire> Render(RenderContext context, LineSource lineSource)
        {
            var wire = new Wire(context);
            await wire.Render(lineSource);
            return wire;
        }

        public async Task Render(LineSource lineSource)
        {
            var type = await lineSource.Read("Wire", "Bus");
            await lineSource.Read("Line");
            await lineSource.Read(TokenType.LineBreak);
            var x1 = await lineSource.Read(typeof(int));
            var y1 = await lineSource.Read(typeof(int));
            var x2 = await lineSource.Read(typeof(int));
            var y2 = await lineSource.Read(typeof(int));
            await lineSource.Read(TokenType.LineBreak);

            await Writer.WriteStartElementAsync("line");

            await Writer.WriteInheritedAttributeStringAsync("class", type.ToLowerInvariant());

            await Writer.WriteNonInheritedAttributeStringAsync("x1", x1);
            await Writer.WriteNonInheritedAttributeStringAsync("y1", y1);
            await Writer.WriteNonInheritedAttributeStringAsync("x2", x2);
            await Writer.WriteNonInheritedAttributeStringAsync("y2", y2);

            if (type == "Bus")
            {
                await Writer.WriteInheritedAttributeStringAsync("stroke", "rgb(0,0,132)");
                await Writer.WriteInheritedAttributeStringAsync("stroke-width", Settings.DefaultStrokeWidth * 2);
            }
            else
            {
                await Writer.WriteInheritedAttributeStringAsync("stroke", "rgb(0,132,0)");
            }

            await Writer.WriteEndElementAsync("line");
        }
    }
}