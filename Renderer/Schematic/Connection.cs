using System.Threading.Tasks;

namespace KiCadDoxer.Renderer.Schematic
{
    internal class Connection : RenderItem
    {
        public Connection(RenderContext renderContext) : base(renderContext)
        {
        }

        public int X { get; private set; }

        public int Y { get; private set; }

        public static async Task<Connection> Render(RenderContext context)
        {
            var connect = new Connection(context);
            await connect.Render();
            return connect;
        }

        public async Task Render()
        {
            await LineSource.Read("~");
            X = await LineSource.Read(typeof(int));
            Y = await LineSource.Read(typeof(int));
            await LineSource.Read(TokenType.LineBreak);
            await Writer.WriteStartElementAsync("circle");
            await Writer.WriteNonInheritedAttributeStringAsync("cx", X);
            await Writer.WriteNonInheritedAttributeStringAsync("cy", Y);
            await Writer.WriteNonInheritedAttributeStringAsync("r", 20);
            await Writer.WriteInheritedAttributeStringAsync("stroke", "none");
            await Writer.WriteInheritedAttributeStringAsync("fill", "rgb(0,132,0)");
            await Writer.WriteNonInheritedAttributeStringAsync("class", "connection");
            await Writer.WriteEndElementAsync("circle");
        }
    }
}