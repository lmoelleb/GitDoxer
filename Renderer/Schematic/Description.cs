using System;
using System.Globalization;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer.Schematic
{
    internal class Description : RenderItem
    {
        private Description(RenderContext renderContext) : base(renderContext)
        {
        }

        public static async Task<Description> Render(RenderContext context, LineSource lineSource)
        {
            var description = new Description(context);
            await description.Render(lineSource);
            return description;
        }

        public async Task Render(LineSource lineSource)
        {
            await lineSource.Read(TokenType.Atom); // Paper size, for example "A4"

            var width = await lineSource.Read(typeof(int));
            var height = await lineSource.Read(typeof(int));
            await lineSource.Read(TokenType.LineBreak);

            Func<double, string> toMM = mils => (mils * 0.0254).ToString(CultureInfo.InvariantCulture) + "mm";

            await Writer.WriteNonInheritedAttributeStringAsync("width", toMM(width));
            await Writer.WriteNonInheritedAttributeStringAsync("height", toMM(height));
            await Writer.WriteNonInheritedAttributeStringAsync("viewBox", $"0 0 {width} {height}");

            // For now no page properties besides side are used
            await lineSource.SkipToLineStartingWith("$EndDescr");
            await lineSource.SkipToStartOfNextLine();
        }
    }
}