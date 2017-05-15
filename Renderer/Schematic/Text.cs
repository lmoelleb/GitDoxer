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

        public int Angle { get; private set; }

        public TextSettings TextSettings { get; private set; }

        public Token Value { get; private set; }

        public int X { get; private set; }

        public int Y { get; private set; }

        protected abstract string CssClass { get; }

        protected virtual bool HasShape => true;

        protected virtual bool IsTextHorizontalAligmentInversed => false;

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

            await text.Render();
            return text;
        }

        protected virtual Task RenderShape() => Task.CompletedTask;

        private async Task Render()
        {
            // Initialize the base class first before calling "Render" on the child items. This is
            // not done as a virtual method to ensure the base is always rendered without having to
            // call base.Render from the derived classes. Yes, it can be argued both ways...
            TextSettings = new TextSettings();

            TextSettings.Stroke = Stroke;

            X = await LineSource.Read(typeof(int));
            Y = await LineSource.Read(typeof(int));

            TextSettings.VerticalJustify = VerticalJustify;

            // Double cast. A bit dodgy and should be refactored... but then... it is a bit funny :)
            var orientation = (Orientation)(int)(await LineSource.Read(0, 1, 2, 3));

            TextSettings.Size = await LineSource.Read(typeof(int));
            Shape shape = Shape.Unspecified;

            if (HasShape)
            {
                shape = (await LineSource.Read(typeof(Shape))).ToEnum<Shape>();
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

            (int dx, int dy) = UnrotatedTextOffset;

            switch (orientation)
            {
                case Orientation.Left:
                    TextSettings.HorizontalJustify = TextHorizontalJustify.Right;
                    dx = -dx;
                    break;

                case Orientation.Up:
                    Angle = 270;
                    int temp = dx;
                    dx = dy; // Vertical text is always offset to the left in KiCad
                    dy = temp;
                    break;

                case Orientation.Down:
                    Angle = 270;
                    TextSettings.HorizontalJustify = TextHorizontalJustify.Right;

                    temp = dx;
                    dx = dy; // Vertical text is always offset to the left in KiCad
                    dy = temp;
                    break;
            }

            if (IsTextHorizontalAligmentInversed)
            {
                TextSettings.HorizontalJustify = TextSettings.HorizontalJustify.GetInverse();
            }

            if (HasShape)
            {
                await Writer.WriteStartElementAsync("g");
                await Writer.WriteInheritedAttributeStringAsync("stroke", TextSettings.StrokeWidth);
                await Writer.WriteInheritedAttributeStringAsync("stroke-width", TextSettings.StrokeWidth);
                await Writer.WriteNonInheritedAttributeStringAsync("class", CssClass);
            }
            else
            {
                TextSettings.ClassNames = CssClass;
            }

            await Writer.WriteTextAsync(X + dx, Y + dy, Angle, Value, TextSettings);

            if (HasShape)
            {
                await Writer.WriteEndElementAsync("g");
            }
        }
    }
}