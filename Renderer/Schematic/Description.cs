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

        public static async Task<Description> Render(RenderContext context)
        {
            var description = new Description(context);
            await description.Render();
            return description;
        }

        private async Task Render()
        {
            await LineSource.Read(TokenType.Atom); // Paper size, for example "A4"

            var width = await LineSource.Read(typeof(int));
            var height = await LineSource.Read(typeof(int));
            await LineSource.Read(TokenType.LineBreak);

            Func<double, string> toMM = mils => (mils * 0.0254).ToString(CultureInfo.InvariantCulture) + "mm";

            await Writer.WriteNonInheritedAttributeStringAsync("width", toMM(width));
            await Writer.WriteNonInheritedAttributeStringAsync("height", toMM(height));
            await Writer.WriteNonInheritedAttributeStringAsync("viewBox", $"0 0 {width} {height}");

            // For now no page properties besides side are used
            await LineSource.SkipToLineStartingWith("$EndDescr");
            await LineSource.SkipToStartOfNextLine();
        }
    }
}