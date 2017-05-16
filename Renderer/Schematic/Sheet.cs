using System.Globalization;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer.Schematic
{
    internal class Sheet : RenderItem
    {
        protected Sheet(RenderContext renderContext) : base(renderContext)
        {
        }

        public int Height { get; private set; }

        public int Width { get; private set; }

        public int X { get; private set; }

        public int Y { get; private set; }

        public static async Task<Sheet> Render(RenderContext context)
        {
            var sheet = new Sheet(context);
            await sheet.Render();
            return sheet;
        }

        private async Task Render()
        {
            await Writer.WriteStartElementAsync("g");
            await Writer.WriteNonInheritedAttributeStringAsync("class", "sheet");
            await Writer.WriteInheritedAttributeStringAsync("stroke", "rgb(132,132,0)");
            await Writer.WriteInheritedAttributeStringAsync("stroke-width", Settings.DefaultStrokeWidth);

            await LineSource.Read(TokenType.LineBreak);
            await LineSource.SkipEmptyLines();
            await LineSource.Read("S");
            X = await LineSource.Read(typeof(int));
            Y = await LineSource.Read(typeof(int));
            Width = await LineSource.Read(typeof(int));
            Height = await LineSource.Read(typeof(int));
            await LineSource.Read(TokenType.LineBreak);

            await Writer.WriteStartElementAsync("rect");
            await Writer.WriteNonInheritedAttributeStringAsync("x", X);
            await Writer.WriteNonInheritedAttributeStringAsync("y", Y);
            await Writer.WriteNonInheritedAttributeStringAsync("width", Width);
            await Writer.WriteNonInheritedAttributeStringAsync("height", Height);
            await Writer.WriteEndElementAsync("rect");

            await LineSource.SkipEmptyLines();

            if (await LineSource.Peek("U", "F0", "$EndSheet") == "U")
            {
                // Not sure what this is, did not see it documented... whatever :)
                await LineSource.Read("U");
                await LineSource.Read(typeof(string));
                await LineSource.Read(TokenType.LineBreak);
            }

            int index = 0;
            while (true)
            {
                await LineSource.SkipEmptyLines();

                // Forces the fields to be in order. I suspect they always will be, though the file
                // format in theory might not require it.
                string lineType = await LineSource.Read("$EndSheet", "F" + index.ToString(CultureInfo.InvariantCulture));

                if (lineType == "$EndSheet")
                {
                    await LineSource.Read(TokenType.LineBreak);
                    break;
                }

                // Could reuse textSettings between most fields, but then I would have to deal with
                // size changes.... could even reuse between calls... later... maybe
                TextSettings textSettings = new TextSettings();
                textSettings.Stroke = index == 0 ? "rgb(0,132,132)" : "rgb(132,132,0)";
                textSettings.StrokeWidth = Settings.DefaultStrokeWidth;

                Text text;

                string value = await LineSource.Read(typeof(string));
                int angle = 0;
                if (index < 2)
                {
                    // Sheet or file name
                    textSettings.Size = await LineSource.Read(typeof(int));
                    int yField = Y;
                    if (index == 1)
                    {
                        // A bit of a hack - the file name is written under the frame, but we still
                        // want a small offset so the top of the letters do not hit the actual frame.
                        // By pretending to render 180 degrees, the "normal label offset" will move
                        // the text down instead of up. By then specifying the text should
                        // automatically be turned to be oriented so it is not upside down the
                        // rendering will happen at the desired location.
                        yField += Height;
                        textSettings.VerticalJustify = TextVerticalJustify.Bottom;
                        textSettings.HorizontalJustify = TextHorizontalJustify.Right;
                        textSettings.AutoRotateUpsideDownText = true;
                        angle = 180;
                    }

                    string prefix = index == 0 ? "Sheet: " : "File: ";
                    textSettings.ClassNames = index == 0 ? "title" : "file";

                    text = new TextLabel(RenderContext, X, yField, angle, prefix + value, textSettings);

                    await text.Render();
                }
                else
                {
                    Shape shape = (await LineSource.Read(typeof(Shape))).ToEnum<Shape>();
                    Side side = (await LineSource.Read(typeof(Side))).ToEnum<Side>();

                    int x = await LineSource.Read(typeof(int));
                    int y = await LineSource.Read(typeof(int));
                    int size = await LineSource.Read(typeof(int));
                }

                index++;
            }

            await Writer.WriteEndElementAsync("g");
        }
    }
}