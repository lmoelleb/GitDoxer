using System;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer.Schematic
{
    internal abstract class Text : RenderItem
    {
        protected const int TextMargin = 4;

        protected Text(RenderContext renderContext) : base(renderContext)
        {
        }

        protected Text(RenderContext renderContext, int x, int y, int angle, string text, TextSettings textSettings)
            : base(renderContext)
        {
            this.Angle = angle;
            this.X = x;
            this.Y = y;
            this.Value = text;
            this.TextSettings = textSettings;
        }

        public int Angle { get; private set; }

        public TextSettings TextSettings { get; private set; }

        public string Value { get; private set; }

        public int X { get; private set; }

        public int Y { get; private set; }

        protected abstract string CssClass { get; }

        protected virtual bool IsTextHorizontalAligmentInversed => false;

        protected Shape ShapeToRender { get; private set; } = Shape.Unspecified;

        protected abstract string Stroke { get; }

        protected virtual (int dx, int dy) UnrotatedTextOffset => (0, -(int)Math.Round(TextMargin + (TextSettings.StrokeWidth + Settings.DefaultStrokeWidth) / 2));

        protected virtual TextVerticalJustify VerticalJustify => TextVerticalJustify.Center;

        public static async Task<Text> Render(RenderContext context)
        {
            var lineSource = context.LineSource;
            var type = await lineSource.Read("Notes", "GLabel", "HLabel", "Label");
            Text text;
            switch ((string)type)
            {
                case "HLabel":
                    text = new TextHierarchicalLabel(context);
                    break;

                case "Label":
                    text = new TextLabel(context);
                    break;

                case "GLabel":
                case "Notes":
                    text = new TextNotes(context);
                    break;

                default:

                    // This should not happen as the token only reads supported types... but keeps
                    // the compiler happy
                    throw new Exception("Unknown text type: " + type);
            }

            await text.Load();
            await text.Render();
            return text;
        }

        internal async Task Render()
        {
            // Initialize the base class first before calling "Render" on the child items. This is
            // not done as a virtual method to ensure the base is always rendered without having to
            // call base.Render from the derived classes. Yes, it can be argued both ways...

            if (this is TextWithShape)
            {
                // Only create the containing group if there are multiple items - so a shape and a
                // text This makes it somewhat dodgy from an OO point of view (checking the type is
                // TextWithShape and using the overload) - consider refactoring, but for now the mess
                // is limited to a few classes and doesn't leak out to the calling classes - so not a
                // high priority.
                await Writer.WriteStartElementAsync("g");
                await Writer.WriteInheritedAttributeStringAsync("stroke", TextSettings.Stroke);
                await Writer.WriteInheritedAttributeStringAsync("stroke-width", TextSettings.StrokeWidth);
                await Writer.WriteNonInheritedAttributeStringAsync("class", CssClass);
                await RenderShape(X, Y, Angle, ShapeToRender);
            }
            else
            {
                TextSettings.ClassNames = CssClass;
            }

            (int dx, int dy) = UnrotatedTextOffset;

            double angleAsRad = Angle * Math.PI / 180.0;

            double dxRotated = dx * Math.Cos(angleAsRad) - dy * Math.Sin(angleAsRad);
            double dyRotated = dx * Math.Sin(angleAsRad) + dy * Math.Cos(angleAsRad);

            int finalX = (int)Math.Round(X + dxRotated);
            int finalY = (int)Math.Round(Y + dyRotated);

            await Writer.WriteTextAsync(finalX, finalY, Angle, Value, TextSettings);

            if (this is TextWithShape)
            {
                await Writer.WriteEndElementAsync("g");
            }
        }

        protected virtual Task RenderShape() => Task.CompletedTask;

        protected virtual Task RenderShape(double x, double y, double angle, Shape shape) => Task.CompletedTask;

        private async Task Load()
        {
            TextSettings = new TextSettings();
            TextSettings.Stroke = Stroke;

            X = await LineSource.Read(typeof(int));
            Y = await LineSource.Read(typeof(int));
            TextSettings.VerticalJustify = VerticalJustify;

            // Double cast. A bit dodgy and should be refactored... but then... it is a bit funny :)
            var orientation = (Orientation)(int)(await LineSource.Read(0, 1, 2, 3));

            TextSettings.Size = await LineSource.Read(typeof(int));
            ShapeToRender = Shape.Unspecified;

            if (this is TextWithShape)
            {
                ShapeToRender = (await LineSource.Read(typeof(Shape))).ToEnum<Shape>();
            }

            await LineSource.Read("~"); // I wonder when I will encounter a text here???

            // There MIGHT be an italic flag here. And/Or the stroke-width... or nothing
            var token = await LineSource.Read("Italic", typeof(int), TokenType.LineBreak);
            if ((string)token == "Italic")
            {
                TextSettings.IsItalic = true;
                token = await LineSource.Read(typeof(int), TokenType.LineBreak);
            }

            if (token.Type == TokenType.Atom)
            {
                TextSettings.StrokeWidth = token;
                if (TextSettings.StrokeWidth == 0)
                {
                    TextSettings.StrokeWidth = Settings.DefaultStrokeWidth;
                }

                await LineSource.Read(TokenType.LineBreak);
            }

            Value = await LineSource.Read(TokenType.LineOfText);
            await LineSource.Read(TokenType.EndOfFile, TokenType.LineBreak);

            switch (orientation)
            {
                case Orientation.Left:
                    TextSettings.HorizontalJustify = TextHorizontalJustify.Right;
                    break;

                case Orientation.Up:
                    Angle = 270;
                    break;

                case Orientation.Down:
                    Angle = 270;
                    TextSettings.HorizontalJustify = TextHorizontalJustify.Right;
                    break;
            }

            if (IsTextHorizontalAligmentInversed)
            {
                TextSettings.HorizontalJustify = TextSettings.HorizontalJustify.GetInverse();
            }
        }
    }
}