using KiCadDoxer.Renderer.Exceptions;
using System.Threading.Tasks;
using Xunit;

namespace KiCadDoxer.Renderer.Tests.Schematic
{
    public class DescriptionTests
    {
        [Fact]
        public async Task ErrorMentionesMissingEnd()
        {
            var testCase = new SchematicTestRenderContext("$Descr A4 200 100", true);
            var ex = await Assert.ThrowsAsync<KiCadFileFormatException>(async () => await testCase.Render());
            Assert.Contains("$EndDescr", ex.Message);
        }

        [Theory]
        [InlineData("$Descr", "text")]
        [InlineData("$Descr A4", "integer")]
        [InlineData("$Descr A4 100", "integer")]
        [InlineData("$Descr A4 100 100", "EndOfFile")]
        public async Task IncompleteLineThrows(string line, string expectedInException)
        {
            var testCase = new SchematicTestRenderContext(line, true);
            var ex = await Assert.ThrowsAsync<KiCadFileFormatException>(async () => await testCase.Render());
            Assert.Contains(expectedInException, ex.Message);
        }

        [Fact]
        public async Task SizeWrittenToOutput()
        {
            var testCase = new SchematicTestRenderContext("$Descr A4 200 100\r\nIgnored Line\r\n$EndDescr", true);
            await testCase.Render();
            Assert.Equal("5.08mm", (string)testCase.Result.Root.Attribute("width"));
            Assert.Equal("2.54mm", (string)testCase.Result.Root.Attribute("height"));
            Assert.Equal("0 0 200 100", (string)testCase.Result.Root.Attribute("viewBox"));
        }
    }
}