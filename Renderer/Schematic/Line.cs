using System.Threading.Tasks;

namespace KiCadDoxer.Renderer.Schematic
{
    internal class Line : RenderItem
    {
        private Line(RenderContext renderContext) : base(renderContext)
        {
        }

        public static async Task<Line> Render(RenderContext context)
        {
            var wire = new Line(context);
            await wire.Render();
            return wire;
        }

        public async Task Render()
        {
            var type = await LineSource.Read("Wire", "Bus", "Notes");
            await LineSource.Read("Line");
            await LineSource.Read(TokenType.LineBreak);
            var x1 = await LineSource.Read(typeof(int));
            var y1 = await LineSource.Read(typeof(int));
            var x2 = await LineSource.Read(typeof(int));
            var y2 = await LineSource.Read(typeof(int));
            await LineSource.Read(TokenType.LineBreak);

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
            else if (type == "Notes")
            {
                await Writer.WriteInheritedAttributeStringAsync("stroke", "rgb(0,0,132)");
                await Writer.WriteInheritedAttributeStringAsync("stroke-dasharray", "13.685,15.8425"); // Constants lifted from example SVG export from KiCad
            }
            else
            {
                await Writer.WriteInheritedAttributeStringAsync("stroke", "rgb(0,132,0)");
            }

            await Writer.WriteEndElementAsync("line");
        }
    }
}