using KiCadDoxer.Renderer.Schematic;
using System.Threading.Tasks;
using Xunit;

namespace KiCadDoxer.Renderer.Tests.Schematic
{
    public class TextNotesTests : TextTests
    {
        internal override TextVerticalJustify ExpectedTextVerticalJustify => TextVerticalJustify.Bottom;

        protected override string TypeKeyword => "Notes";

        [Fact]
        public async Task NotesAreBlue()
        {
            var testCase = new SchematicTestRenderContext("Notes 1000 2000 0    60   ~ 0\r\nTest\r\n", false);
            var testWriter = new TestFragmentWriter();
            testCase.PushSvgWriter(testWriter);

            await (Text.Render(testCase));

            Assert.Equal("rgb(0,0,132)", testWriter.GetTextSettings("Test").Stroke);
        }
    }
}