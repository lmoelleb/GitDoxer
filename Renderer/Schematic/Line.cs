using System.Threading.Tasks;

namespace KiCadDoxer.Renderer.Schematic
{
    internal class Line : RenderItem
    {
        private Line(RenderContext renderContext) : base(renderContext)
        {
        }

        public int X1 { get; private set; }

        public int X2 { get; private set; }

        public int Y1 { get; private set; }

        public int Y2 { get; private set; }

        public static async Task<Line> Render(RenderContext context)
        {
            var line = new Line(context);
            await line.Render();
            return line;
        }

        public async Task Render()
        {
            var type = await LineSource.Read("Wire", "Bus", "Notes");
            await LineSource.Read("Line");
            await LineSource.Read(TokenType.LineBreak);
            X1 = await LineSource.Read(typeof(int));
            Y1 = await LineSource.Read(typeof(int));
            X2 = await LineSource.Read(typeof(int));
            Y2 = await LineSource.Read(typeof(int));
            await LineSource.Read(TokenType.LineBreak);

            await Writer.WriteStartElementAsync("line");

            await Writer.WriteNonInheritedAttributeStringAsync("class", type.ToLowerInvariant());

            await Writer.WriteNonInheritedAttributeStringAsync("x1", X1);
            await Writer.WriteNonInheritedAttributeStringAsync("y1", Y1);
            await Writer.WriteNonInheritedAttributeStringAsync("x2", X2);
            await Writer.WriteNonInheritedAttributeStringAsync("y2", Y2);

            if (type == "Bus")
            {
                await Writer.WriteInheritedAttributeStringAsync("stroke", "rgb(0,0,132)");
                await Writer.WriteInheritedAttributeStringAsync("stroke-width", Settings.DefaultStrokeWidth * 2);
            }
            else if (type == "Notes")
            {
                await Writer.WriteInheritedAttributeStringAsync("stroke", "rgb(0,0,132)");
                await Writer.WriteInheritedAttributeStringAsync("stroke-dasharray", "13.685,15.8425"); // Constants lifted from example SVG export from KiCad
                await Writer.WriteInheritedAttributeStringAsync("stroke-width", Settings.DefaultStrokeWidth);
            }
            else
            {
                await Writer.WriteInheritedAttributeStringAsync("stroke-width", Settings.DefaultStrokeWidth);
                await Writer.WriteInheritedAttributeStringAsync("stroke", "rgb(0,132,0)");
            }

            await Writer.WriteEndElementAsync("line");
        }
    }
}