using KiCadDoxer.Renderer.Schematic;
using System.Threading.Tasks;
using Xunit;

namespace KiCadDoxer.Renderer.Tests.Schematic
{
    public class TextLabelTests : TextTests
    {
        internal override TextVerticalJustify ExpectedTextVerticalJustify => TextVerticalJustify.Bottom;

        protected override string TypeKeyword => "Notes";

        [Theory]
        [InlineData("0")]
        [InlineData("2")]
        public async Task HorizontalLabelsAreOffsetUpwards(string orientation)
        {
            var testCase = new SchematicTestRenderContext($"Label 0 0 {orientation}    60   ~ 0\r\nTest\r\n", false);
            var testWriter = new TestFragmentWriter();
            testCase.PushSvgWriter(testWriter);

            await (Text.Render(testCase));

            var locatation = testWriter.GetTextLocation("Test");

            Assert.True(locatation.Y < 0);
            Assert.True(locatation.X == 0);
        }

        [Fact]
        public async Task LabelsAreBlack()
        {
            var testCase = new SchematicTestRenderContext("Label 1000 2000 0    60   ~ 0\r\nTest\r\n", false);
            var testWriter = new TestFragmentWriter();
            testCase.PushSvgWriter(testWriter);

            await (Text.Render(testCase));

            Assert.Equal("rgb(0,0,0)", testWriter.GetTextSettings("Test").Stroke);
        }

        [Theory]
        [InlineData("1")]
        [InlineData("3")]
        public async Task VerticalLabelsAreOffsetLeft(string orientation)
        {
            var testCase = new SchematicTestRenderContext($"Label 0 0 {orientation}    60   ~ 0\r\nTest\r\n", false);
            var testWriter = new TestFragmentWriter();
            testCase.PushSvgWriter(testWriter);

            await (Text.Render(testCase));

            var locatation = testWriter.GetTextLocation("Test");

            Assert.True(locatation.X < 0);
            Assert.True(locatation.Y == 0);
        }
    }
}