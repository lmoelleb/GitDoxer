using KiCadDoxer.Renderer.Exceptions;
using KiCadDoxer.Renderer.Schematic;
using System.Threading.Tasks;
using Xunit;

namespace KiCadDoxer.Renderer.Tests.Schematic
{
    public class NoConnectionTests
    {
        [Fact]
        public async Task AllLinesArePresent()
        {
            var testCase = new SchematicTestRenderContext("~ 100 200\n", false);
            var testWriter = new TestFragmentWriter();
            testCase.PushSvgWriter(testWriter);

            await (NoConnection.Render(testCase));

            // Slash
            Assert.True(testWriter.ContainsLine(76, 176, 124, 224));

            // Backslash
            Assert.True(testWriter.ContainsLine(76, 224, 124, 176));

            // Nothing extra
            Assert.Equal(2, testWriter.CountElementsIgnoringAttributes("line"));
        }

        [Fact]
        public async Task CrossIsBlueAndThin()
        {
            var testCase = new SchematicTestRenderContext("~ 100 200\n", false);
            var testWriter = new TestFragmentWriter();
            testCase.PushSvgWriter(testWriter);

            await (NoConnection.Render(testCase));
            Assert.True(testWriter.ContainsElement("g", ("stroke", "rgb(0,0,132)", true), ("stroke-width", "6", true), ("class", "no-connection", false)));
        }

        [Theory]
        [InlineData("FAIL", "~")]
        [InlineData("~", "integer")]
        [InlineData("~ FAIL", "integer")]
        [InlineData("~ 1", "integer")]
        [InlineData("~ 1 FAIL", "integer")]
        [InlineData("~ 1 2 FAIL", "LineBreak")]
        public async Task IncompleteLineOrWrongTokensThrows(string line, string expectedInException)
        {
            var testCase = new SchematicTestRenderContext(line, false);
            var ex = await Assert.ThrowsAsync<KiCadFileFormatException>(async () => await NoConnection.Render(testCase));
            Assert.Contains(expectedInException, ex.Message);
        }
    }
}