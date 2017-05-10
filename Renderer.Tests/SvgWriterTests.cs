using KiCadDoxer.Renderer.Exceptions;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace KiCadDoxer.Renderer.Tests
{
    // Consider something like XMLUnit, but for now the required tests are so simple string
    // comparisons should do. It is not our task to write unit tests verifying an XML writer produce
    // valid XML. :)
    public class SvgWriterTests
    {
        [Fact]
        public async Task ElementStartAndEndShouldBeBalancedWithProperErrorMessage()
        {
            StringWriter writer = new StringWriter();
            using (SvgRootWriter svgWriter = new SvgRootWriter(new TestRenderSettings(), () => Task.FromResult((TextWriter)writer)))
            {
                await svgWriter.WriteStartElementAsync("svg");
                await svgWriter.WriteStartElementAsync("child1");
                await svgWriter.WriteStartElementAsync("child2");
                Exception ex = await Assert.ThrowsAsync<InternalRenderException>(async () => await svgWriter.WriteEndElementAsync("child1"));
                Assert.Contains("child1", ex.Message);
                Assert.Contains("child2", ex.Message);
            }
        }

        [Fact]
        public async Task InheritedAttributeWrittenOnce()
        {
            // Currently comments are ONLY written if the root element is completed. This is not
            // necessarely brilliant, but it works for standard cases so I'll let it slide for now
            StringWriter writer = new StringWriter();
            using (SvgRootWriter svgWriter = new SvgRootWriter(new TestRenderSettings(), () => Task.FromResult((TextWriter)writer)))
            {
                await svgWriter.WriteStartElementAsync("svg");
                await svgWriter.WriteInheritedAttributeStringAsync("attribute_", "val");
                await svgWriter.WriteStartElementAsync("child");
                await svgWriter.WriteInheritedAttributeStringAsync("attribute_", "val");
                await svgWriter.WriteEndElementAsync("child");
                await svgWriter.WriteEndElementAsync("svg");
            }

            string result = writer.ToString();

            // Ugly smugly, but will do the job for now :)
            Assert.Equal(result.Count(c => c == '_'), 1);
        }

        [Fact]
        public async Task NonInheritedAttributeWrittenTwice()
        {
            // Currently comments are ONLY written if the root element is completed. This is not
            // necessarely brilliant, but it works for standard cases so I'll let it slide for now
            StringWriter writer = new StringWriter();
            using (SvgRootWriter svgWriter = new SvgRootWriter(new TestRenderSettings(), () => Task.FromResult((TextWriter)writer)))
            {
                await svgWriter.WriteStartElementAsync("svg");
                await svgWriter.WriteNonInheritedAttributeStringAsync("attribute_", "val");
                await svgWriter.WriteStartElementAsync("child");
                await svgWriter.WriteNonInheritedAttributeStringAsync("attribute_", "val");
                await svgWriter.WriteEndElementAsync("child");
                await svgWriter.WriteEndElementAsync("svg");
            }

            string result = writer.ToString();

            // Ugly smugly, but will do the job for now :)
            Assert.Equal(result.Count(c => c == '_'), 2);
        }

        [Fact]
        public async Task RootMustBeNamedSvg()
        {
            StringWriter writer = new StringWriter();
            using (SvgRootWriter svgWriter = new SvgRootWriter(new TestRenderSettings(), () => Task.FromResult((TextWriter)writer)))
            {
                await svgWriter.WriteStartElementAsync("not_svg");
                await Assert.ThrowsAsync<InternalRenderException>(() => svgWriter.WriteEndElementAsync("not_svg"));
            }
        }

        [Fact]
        public async Task WriteComment()
        {
            // Currently comments are ONLY written if the root element is completed. This is not
            // necessarely brilliant, but it works for standard cases so I'll let it slide for now
            StringWriter writer = new StringWriter();
            using (SvgRootWriter svgWriter = new SvgRootWriter(new TestRenderSettings(), () => Task.FromResult((TextWriter)writer)))
            {
                await svgWriter.WriteStartElementAsync("svg");
                await svgWriter.WriteCommentAsync("comment");
                await svgWriter.WriteEndElementAsync("svg");
            }

            string result = writer.ToString();

            Assert.Contains("<!--comment-->", result);
        }

        [Fact]
        public async Task WriteRootElement()
        {
            StringWriter writer = new StringWriter();
            using (SvgRootWriter svgWriter = new SvgRootWriter(new TestRenderSettings(), () => Task.FromResult((TextWriter)writer)))
            {
                await svgWriter.WriteStartElementAsync("svg");
                await svgWriter.WriteEndElementAsync("svg");
            }

            XDocument result = XDocument.Parse(writer.ToString());
            Assert.Equal("svg", result.Root.Name.LocalName);
            Assert.Equal("http://www.w3.org/2000/svg", result.Root.Name.NamespaceName);
            Assert.Equal(0, result.Root.Elements().Count());
            Assert.Equal(0, result.Root.Attributes().Count(a => !a.IsNamespaceDeclaration));
        }

        [Fact]
        public async Task WriteText()
        {
            StringWriter writer = new StringWriter();
            using (SvgRootWriter svgWriter = new SvgRootWriter(new TestRenderSettings(), () => Task.FromResult((TextWriter)writer)))
            {
                await svgWriter.WriteStartElementAsync("svg");
                await svgWriter.WriteTextNodeAsync("text");
                await svgWriter.WriteEndElementAsync("svg");
            }

            string result = writer.ToString();

            Assert.Contains(">text<", result);
        }

        private class TestRenderSettings : RenderSettings
        {
            public override bool PrettyPrint => false;
        }
    }
}