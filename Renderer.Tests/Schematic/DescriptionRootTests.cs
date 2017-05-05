using KiCadDoxer.Renderer.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace KiCadDoxer.Renderer.Tests.Schematic
{
    public class DescriptionRootTests
    {

        [Theory]
        [InlineData("$Descr", "Atom")]
        [InlineData("$Descr A4", "Atom")]
        [InlineData("$Descr A4 100", "Atom")]
        // Bad error message, need to correct error messages when looking through file searching for a keyword
        [InlineData("$Descr A4 100 100", "Atom")]
        public async Task IncompleteLineThrows(string line, string expectedInException)
        {
            var testCase = new SchematicTestRenderContext(line, true);
            var ex = await Assert.ThrowsAsync<KiCadFileFormatException>(async () => await testCase.Render());
            Assert.Contains(expectedInException, ex.Message);
        }

        [Fact]
        public async Task SizeWrittenToOutput()
        {
            var testCase = new SchematicTestRenderContext("$Descr A4 200 100\r\n$EndDescr", true);
            await testCase.Render();
            Assert.Equal("5.08mm", (string)testCase.Result.Root.Attribute("width"));
            Assert.Equal("2.54mm", (string)testCase.Result.Root.Attribute("height"));
            Assert.Equal("0 0 200 100", (string)testCase.Result.Root.Attribute("viewBox"));
        }
    }
}
