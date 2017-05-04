using System.Threading.Tasks;
using Xunit;

namespace KiCadDoxer.Renderer.Tests.Schematic
{
    public class SchematicRootTests
    {
        [Fact]
        public async Task EmptySchematicGivesEmptySvgRoot()
        {
            var testCase = new SchematicTestRenderContext("EESchema");
            await testCase.Render();
            Assert.Empty(testCase.Result.Root.Elements());
        }
    }
}