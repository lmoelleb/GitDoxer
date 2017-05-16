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

                string value = await LineSource.Read(typeof(string));
                if (index < 2)
                {
                    await RenderSheetTitleOrFile(index, value);
                }
                else
                {
                    await RenderSheetPin(index, value);
                }

                index++;
            }

            await Writer.WriteEndElementAsync("g");
        }


        private async Task RenderSheetPin(int index, string value)
        {
            Shape shape = (await LineSource.Read(typeof(Shape))).ToEnum<Shape>();
            Side side = (await LineSource.Read(typeof(Side))).ToEnum<Side>();

            int x = await LineSource.Read(typeof(int));
            int y = await LineSource.Read(typeof(int));
            int size = await LineSource.Read(typeof(int));
            int angle = 0;

            TextSettings textSettings = new TextSettings
            {
                ClassNames = "hpin",
                Stroke = "rgb(132,132,0)",
                Size = size,
                StrokeWidth = Settings.DefaultStrokeWidth,
                VerticalJustify = TextVerticalJustify.Center
            };


            // OK, now it get's ugly, please close your eyes
            // A pin inside a sheet is rendered identical to a hierarchical label, except the
            // shape must point in the opposite direction as this is "going inside the box" instead
            // of "going outside the sheet".
            // The sensible thing is to have generic code that renders this shape and text and use this
            // from both the HLabel and the sheet. But that requires refactoring - which is fine once
            // it is clear how to get a decent OO structure after the refactoring. As this is not clear
            // to me right now (and not a priority to get clear) I am going to simply make the sheet
            // "conform to the HLabel requirements" - so basically use the HLabel renderer as is, with
            // the inputs hacked to give the desired outcome.

            if (shape == Shape.Input)
            {
                shape = Shape.Output;
            }
            else if (shape == Shape.Output)
            {
                shape = Shape.Input;
            }

            textSettings.AutoRotateUpsideDownText = true;
            textSettings.HorizontalJustify = TextHorizontalJustify.Right;
            switch (side)
            {
                case Side.Left:
                    angle = 180;
                    break;
                case Side.Bottom:
                    angle = 270;
                    break;
                case Side.Top:
                    angle = 90;
                    break;
            }

            TextHierarchicalLabel hLabel = new TextHierarchicalLabel(RenderContext, x, y, angle, value, shape, textSettings);
            await hLabel.Render();
    }

    private async Task RenderSheetTitleOrFile(int index, string value)
        {
            TextSettings textSettings = new TextSettings();
            textSettings.Stroke = index == 0 ? "rgb(0,132,132)" : "rgb(132,132,0)";
            textSettings.StrokeWidth = Settings.DefaultStrokeWidth;


            Text text;
            // Sheet or file name
            textSettings.Size = await LineSource.Read(typeof(int));
            int yField = Y;
            int angle = 0;

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
    }
}