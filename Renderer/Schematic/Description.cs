using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer.Schematic
{
    internal class Description : RenderItem
    {
        public Description(RenderContext renderContext) : base(renderContext)
        {
        }

        public async Task Render(LineSource lineSource)
        {
            await lineSource.Read(TokenType.Atom); // Paper size, for example "A4"

            var width = await lineSource.Read(TokenType.Atom);
            var height = await lineSource.Read(TokenType.Atom);
            await lineSource.Read(TokenType.LineBreak);

            Func<double, string> toMM = mils => (mils * 0.0254).ToString(CultureInfo.InvariantCulture) + "mm";

            await Writer.WriteNonInheritedAttributeStringAsync("width", toMM(width));
            await Writer.WriteNonInheritedAttributeStringAsync("height", toMM(height));
            await Writer.WriteNonInheritedAttributeStringAsync("viewBox", $"0 0 {width} {height}");

            // Skip the rest of the description for now
            bool descriptionCompleted = false;
            while (!descriptionCompleted)
            {
                var token = await lineSource.Read(TokenType.Atom);
                if (token == "$EndDescr")
                {
                    await lineSource.Read(TokenType.LineBreak);
                    descriptionCompleted = true;
                }
                else
                {
                    await lineSource.SkipToStartOfNextLine();
                }
            }
        }

        public static async Task<Description> Render(RenderContext context, LineSource lineSource)
        {
            var description = new Description(context);
            await description.Render(lineSource);
            return description;
        }
    }
}
