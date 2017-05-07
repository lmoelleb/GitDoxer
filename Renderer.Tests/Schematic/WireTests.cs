using KiCadDoxer.Renderer.Exceptions;
using KiCadDoxer.Renderer.Schematic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace KiCadDoxer.Renderer.Tests.Schematic
{
    public class WireTests
    {
        [Fact]
        public async Task BussesAreBlueAndFat()
        {
            var testCase = new SchematicTestRenderContext("Wire Bus Line\r\n1 2 3 4\r\n", true);
            await testCase.Render();
            var svgLine = testCase.Result.Root.Elements().Single();
            Assert.Equal(svgLine.Name.LocalName, "line");
            Assert.Equal("rgb(0,0,132)", (string)svgLine.Attribute("stroke"));
            Assert.Equal("1", (string)svgLine.Attribute("x1"));
            Assert.Equal("2", (string)svgLine.Attribute("y1"));
            Assert.Equal("3", (string)svgLine.Attribute("x2"));
            Assert.Equal("4", (string)svgLine.Attribute("y2"));
            Assert.Equal(testCase.SchematicRenderSettings.DefaultStrokeWidth * 2, (double)svgLine.Attribute("stroke-width"));
        }

        [Theory]
        [InlineData("", "Wire")]
        [InlineData("", "Bus")]
        [InlineData("", "Notes")]
        [InlineData("FAIL", "Bus")]
        [InlineData("Wire", "Line")]
        [InlineData("Wire FAIL", "Line")]
        [InlineData("Wire Line", "LineBreak")]
        [InlineData("Wire Line FAIL", "LineBreak")]
        [InlineData("Wire Line\n1", "integer")]
        [InlineData("Wire Line\n1 FAIL", "integer")]
        [InlineData("Wire Line\n1 2", "integer")]
        [InlineData("Wire Line\n1 2 FAIL", "integer")]
        [InlineData("Wire Line\n1 2 3", "integer")]
        [InlineData("Wire Line\n1 2 3 FAIL", "integer")]
        [InlineData("Wire Line\n1 2 3 4 FAIL", "LineBreak")]
        public async Task IncompleteLineOrWrongTokensThrows(string line, string expectedInException)
        {
            var testCase = new SchematicTestRenderContext(line, false);
            var ex = await Assert.ThrowsAsync<KiCadFileFormatException>(async () => await Line.Render(testCase));
            Assert.Contains(expectedInException, ex.Message);
        }

        [Fact]
        public async Task WiresAreGreenAndThin()
        {
            var testCase = new SchematicTestRenderContext("Wire Wire Line\r\n1 2 3 4\r\n", true);
            await testCase.Render();
            var svgLine = testCase.Result.Root.Elements().Single();
            Assert.Equal(svgLine.Name.LocalName, "line");
            Assert.Equal("rgb(0,132,0)", (string)svgLine.Attribute("stroke"));
            Assert.Equal("1", (string)svgLine.Attribute("x1"));
            Assert.Equal("2", (string)svgLine.Attribute("y1"));
            Assert.Equal("3", (string)svgLine.Attribute("x2"));
            Assert.Equal("4", (string)svgLine.Attribute("y2"));
            Assert.Null(svgLine.Attribute("stroke-width")); // Should use default
        }

        [Fact]
        public async Task NotesAreBlueThinAndDashed()
        {
            var testCase = new SchematicTestRenderContext("Wire Notes Line\r\n1 2 3 4\r\n", true);
            await testCase.Render();
            var svgLine = testCase.Result.Root.Elements().Single();
            Assert.Equal(svgLine.Name.LocalName, "line");
            Assert.Equal("rgb(0,0,132)", (string)svgLine.Attribute("stroke"));
            Assert.Equal("136.85,158.425", (string)svgLine.Attribute("stroke-dasharray"));
            Assert.Equal("1", (string)svgLine.Attribute("x1"));
            Assert.Equal("2", (string)svgLine.Attribute("y1"));
            Assert.Equal("3", (string)svgLine.Attribute("x2"));
            Assert.Equal("4", (string)svgLine.Attribute("y2"));
            Assert.Null(svgLine.Attribute("stroke-width")); // Should use default
        }
    }
}