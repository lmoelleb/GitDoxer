using KiCadDoxer.Renderer.Exceptions;
using KiCadDoxer.Renderer.Schematic;
using System.Threading.Tasks;
using Xunit;

namespace KiCadDoxer.Renderer.Tests.Schematic
{
    // Say the name of this class 10 times fast :P
    public abstract class TextTests
    {
        internal virtual TextVerticalJustify ExpectedTextVerticalJustify => TextVerticalJustify.Center;

        protected abstract string TypeKeyword { get; }

        [Theory]
        [InlineData("", "KEYWORD")]
        [InlineData("FAIL", "KEYWORD")]
        [InlineData("KEYWORD", "integer")]
        [InlineData("KEYWORD FAIL", "integer")]
        [InlineData("KEYWORD 1", "integer")]
        [InlineData("KEYWORD 1 FAIL", "integer")]
        [InlineData("KEYWORD 1 2", "0")]
        [InlineData("KEYWORD 1 2", "1")]
        [InlineData("KEYWORD 1 2", "2")]
        [InlineData("KEYWORD 1 2", "3")]
        [InlineData("KEYWORD 1 2 FAIL", "0")]
        [InlineData("KEYWORD 1 2 FAIL", "1")]
        [InlineData("KEYWORD 1 2 FAIL", "2")]
        [InlineData("KEYWORD 1 2 FAIL", "3")]
        [InlineData("KEYWORD 1 2 0", "integer")]
        [InlineData("KEYWORD 1 2 0 FAIL", "integer")]
        [InlineData("KEYWORD 1 2 0 60", "~")]
        [InlineData("KEYWORD 1 2 0 60 FAIL", "~")]
        [InlineData("KEYWORD 1 2 0 60 ~", "Italic")]
        [InlineData("KEYWORD 1 2 0 60 ~", "integer")]
        [InlineData("KEYWORD 1 2 0 60 ~", "LineBreak")]
        [InlineData("KEYWORD 1 2 0 60 ~ FAIL", "Italic")]
        [InlineData("KEYWORD 1 2 0 60 ~ FAIL", "integer")]
        [InlineData("KEYWORD 1 2 0 60 ~ FAIL", "LineBreak")]
        [InlineData("KEYWORD 1 2 0 60 ~ Italic", "integer")]
        [InlineData("KEYWORD 1 2 0 60 ~ Italic", "LineBreak")]
        [InlineData("KEYWORD 1 2 0 60 ~ Italic FAIL", "integer")]
        [InlineData("KEYWORD 1 2 0 60 ~ Italic FAIL", "LineBreak")]
        [InlineData("KEYWORD 1 2 0 60 ~ Italic 12", "LineBreak")]
        [InlineData("KEYWORD 1 2 0 60 ~ Italic 12 FAIL", "LineBreak")]
        [InlineData("KEYWORD 1 2 0 60 ~ 12", "LineBreak")]
        [InlineData("KEYWORD 1 2 0 60 ~ 12 FAIL", "LineBreak")]
        public async Task IncompleteLineOrWrongTokensThrows(string line, string expectedInException)
        {
            line = line.Replace("KEYWORD", TypeKeyword);
            expectedInException = expectedInException.Replace("KEYWORD", TypeKeyword);
            var testCase = new SchematicTestRenderContext(line, false);
            var ex = await Assert.ThrowsAsync<KiCadFileFormatException>(async () => await Text.Render(testCase));
            Assert.Contains(expectedInException, ex.Message);
        }

        [Fact]
        public async Task ItalicCanBeSpecified()
        {
            string line = "KEYWORD 1 2 0 60 ~ Italic 0\nText\n".Replace("KEYWORD", TypeKeyword);
            var testCase = new SchematicTestRenderContext(line, false);
            var testWriter = new TestFragmentWriter();
            testCase.PushSvgWriter(testWriter);

            await Text.Render(testCase);
            Assert.True(testWriter.GetTextSettings("Text").IsItalic);
        }

        [Fact]
        public async Task SizeCanBeSet()
        {
            string line = "KEYWORD 1 2 0 42 ~ 0\nText\n".Replace("KEYWORD", TypeKeyword);
            var testCase = new SchematicTestRenderContext(line, false);
            var testWriter = new TestFragmentWriter();
            testCase.PushSvgWriter(testWriter);

            await Text.Render(testCase);
            Assert.Equal(42, testWriter.GetTextSettings("Text").Size);
        }

        [Fact]
        public async Task StrokeWidthCanBeDefault()
        {
            string line = "KEYWORD 1 2 0 60 ~ 0\nText\n".Replace("KEYWORD", TypeKeyword);
            var testCase = new SchematicTestRenderContext(line, false);
            var testWriter = new TestFragmentWriter();
            testCase.PushSvgWriter(testWriter);

            await Text.Render(testCase);
            Assert.Equal(6, testWriter.GetTextSettings("Text").StrokeWidth);
        }

        [Fact]
        public async Task StrokeWidthCanBeSet()
        {
            string line = "KEYWORD 1 2 0 60 ~ 12\nText\n".Replace("KEYWORD", TypeKeyword);
            var testCase = new SchematicTestRenderContext(line, false);
            var testWriter = new TestFragmentWriter();
            testCase.PushSvgWriter(testWriter);

            await Text.Render(testCase);
            Assert.Equal(12, testWriter.GetTextSettings("Text").StrokeWidth);
        }

        [Fact]
        public async Task TextIsRenderedFromSchematicRenderer()
        {
            string line = "Text KEYWORD 1 2 0 60 ~ 12\r\nTestText".Replace("KEYWORD", TypeKeyword);
            var testCase = new SchematicTestRenderContext(line, true);

            await testCase.Render();

            // A bit dodgy - just look for the comment included with the text node. But will do :)
            Assert.True(testCase.Result.ToString().Contains("TestText"));
        }

        [Fact]
        public async Task TextLineCanBeLastLine()
        {
            string line = "KEYWORD 1 2 0 60 ~ 12\nText\n".Replace("KEYWORD", TypeKeyword);
            var testCase = new SchematicTestRenderContext(line, false);
            var testWriter = new TestFragmentWriter();
            testCase.PushSvgWriter(testWriter);

            await Text.Render(testCase);
            Assert.True(testWriter.ContainsText("Text"));
        }

        [Fact]
        public async Task TextLineCanBeTerminatedByEndOfFile()
        {
            string line = "KEYWORD 1 2 0 60 ~ 12\r\nText".Replace("KEYWORD", TypeKeyword);
            var testCase = new SchematicTestRenderContext(line, false);
            var testWriter = new TestFragmentWriter();
            testCase.PushSvgWriter(testWriter);

            await Text.Render(testCase);
            Assert.True(testWriter.ContainsText("Text"));
        }

        [Theory]
        [InlineData("1")]
        [InlineData("2")]
        [InlineData("3")]
        internal async Task RotatedTextOffsetMatchOrientation(string orientation)
        {
            // Any Y offset must always be upwards or left as this offset is only used to move Labels
            // and NOtes a bit away from the grid they are placed on - to avoid the baseline of the
            // text to be directly on top of wires etc on the grid.

            // The X offset on the other hand is used to make room for a shape, so it must follow the
            // text direction.

            string line = "KEYWORD 0 0 ORIENTATION 60 ~ 0\nText\n".Replace("KEYWORD", TypeKeyword).Replace("ORIENTATION", orientation);
            var testCase = new SchematicTestRenderContext(line, false);
            var testWriter = new TestFragmentWriter();
            testCase.PushSvgWriter(testWriter);

            await Text.Render(testCase);

            // Get the unrotated reference:
            line = "KEYWORD 0 0 0 60 ~ 0\nText\n".Replace("KEYWORD", TypeKeyword);
            var referenceCase = new SchematicTestRenderContext(line, false);
            var referenceWriter = new TestFragmentWriter();
            referenceCase.PushSvgWriter(referenceWriter);
            await Text.Render(referenceCase);

            var location = testWriter.GetTextLocation("Text");
            var referenceLocation = referenceWriter.GetTextLocation("Text");

            switch (orientation)
            {
                case "1":

                    // Up
                    Assert.Equal(referenceLocation.Y, location.X);
                    Assert.Equal(-referenceLocation.X, location.Y);
                    break;

                case "2":

                    // Left
                    Assert.Equal(-referenceLocation.X, location.X);
                    Assert.Equal(referenceLocation.Y, location.Y);
                    break;

                case "3":

                    // Down
                    Assert.Equal(referenceLocation.Y, location.X);
                    Assert.Equal(referenceLocation.X, location.Y);
                    break;
            }
        }

        [Theory]
        [InlineData("0", 0, TextHorizontalJustify.Left)]
        [InlineData("1", 270, TextHorizontalJustify.Left)]
        [InlineData("2", 0, TextHorizontalJustify.Right)]
        [InlineData("3", 270, TextHorizontalJustify.Right)]
        internal async Task TextCanRenderInDirection(string orientation, double expectedAngle, TextHorizontalJustify expectedHorizontalJustify)
        {
            string line = "KEYWORD 1 2 ORIENTATION 60 ~ Italic 0\nText\n".Replace("KEYWORD", TypeKeyword).Replace("ORIENTATION", orientation);
            var testCase = new SchematicTestRenderContext(line, false);
            var testWriter = new TestFragmentWriter();
            testCase.PushSvgWriter(testWriter);

            await Text.Render(testCase);
            var location = testWriter.GetTextLocation("Text");
            Assert.Equal(expectedAngle, location.Angle);
            var settings = testWriter.GetTextSettings("Text");
            Assert.Equal(expectedHorizontalJustify, settings.HorizontalJustify);
        }
    }
}