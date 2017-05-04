using KiCadDoxer.Renderer.Exceptions;
using System.Threading.Tasks;
using Xunit;

namespace KiCadDoxer.Renderer.Tests.Schematic
{
    public class SchematicRootTests
    {

        [Fact]
        public async Task WrongVersionThrowsException()
        {
            var testCase = new SchematicTestRenderContext("EESchema Schematic File Version VERSION");
            var ex = await Assert.ThrowsAsync<KiCadFileFormatException>(async () => await testCase.Render());
            // Ugly checking the text of the exception, should introduce sub type
            Assert.Contains("VERSION",  ex.Message);
        }

        [Theory]
        [InlineData("", "EESchema")]
        [InlineData("EESchema", "Schematic")]
        [InlineData("EESchema Schematic", "File")]
        [InlineData("EESchema Schematic File", "Version")]
        // Not the best of error messages this one, but it is what we currently expect :)
        [InlineData("EESchema Schematic File Version", "Atom")]
        [InlineData("EESchema Schematic File Version 2", "EndOfFile")]
        public async Task IncompleteFirstLineThrows(string line, string expectedInException)
        {
            var testCase = new SchematicTestRenderContext(line);
            var ex = await Assert.ThrowsAsync<KiCadFileFormatException>(async () => await testCase.Render());
            Assert.Contains(expectedInException, ex.Message);
        }

        [Fact]
        public async Task EmptySchematicGivesEmptySvgRoot()
        {
            var testCase = new SchematicTestRenderContext("EESchema Schematic File Version 2\r\n$EndSCHEMATC");
            await testCase.Render();
            Assert.Empty(testCase.Result.Root.Elements());
        }
    }
}