using KiCadDoxer.Renderer.Exceptions;
using KiCadDoxer.Renderer.Schematic;
using System.Threading.Tasks;
using Xunit;

namespace KiCadDoxer.Renderer.Tests.Schematic
{
    public class ConnectionTests
    {
        [Fact]
        public async Task CircleIsGreen()
        {
            var testCase = new SchematicTestRenderContext("~ 100 200\n", false);
            var testWriter = new TestFragmentWriter();
            testCase.PushSvgWriter(testWriter);

            await (Connection.Render(testCase));
            Assert.True(testWriter.ContainsElement("circle", ("cx", "100", false), ("cy", "200", false), ("r", "25", false), ("fill", "rgb(0,132,0)", true), ("stroke", "none", true), ("class", "connection", false)));
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
            var ex = await Assert.ThrowsAsync<KiCadFileFormatException>(async () => await Connection.Render(testCase));
            Assert.Contains(expectedInException, ex.Message);
        }
    }
}