using System.Threading.Tasks;

namespace KiCadDoxer.Renderer.Schematic
{
    internal class NoConnection : RenderItem
    {
        public NoConnection(RenderContext renderContext) : base(renderContext)
        {
        }

        public int X { get; private set; }

        public int Y { get; private set; }

        public static async Task<NoConnection> Render(RenderContext context)
        {
            var noConnect = new NoConnection(context);
            await noConnect.Render();
            return noConnect;
        }

        public async Task Render()
        {
            await LineSource.Read("~");
            X = await LineSource.Read(typeof(int));
            Y = await LineSource.Read(typeof(int));
            await LineSource.Read(TokenType.LineBreak);

            await Writer.WriteStartElementAsync("g");
            await Writer.WriteNonInheritedAttributeStringAsync("class", "no-connection");

            await Writer.WriteInheritedAttributeStringAsync("stroke", "rgb(0,0,132)");
            await Writer.WriteInheritedAttributeStringAsync("stroke-width", Settings.DefaultStrokeWidth);

            await Writer.WriteStartElementAsync("line");
            await Writer.WriteNonInheritedAttributeStringAsync("x1", X + 24);
            await Writer.WriteNonInheritedAttributeStringAsync("y1", Y + 24);
            await Writer.WriteNonInheritedAttributeStringAsync("x2", X - 24);
            await Writer.WriteNonInheritedAttributeStringAsync("y2", Y - 24);
            await Writer.WriteEndElementAsync("line");

            await Writer.WriteStartElementAsync("line");
            await Writer.WriteNonInheritedAttributeStringAsync("x1", X - 24);
            await Writer.WriteNonInheritedAttributeStringAsync("y1", Y + 24);
            await Writer.WriteNonInheritedAttributeStringAsync("x2", X + 24);
            await Writer.WriteNonInheritedAttributeStringAsync("y2", Y - 24);
            await Writer.WriteEndElementAsync("line");

            await Writer.WriteEndElementAsync("g");
        }
    }
}