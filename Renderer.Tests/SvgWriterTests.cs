using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace KiCadDoxer.Renderer.Tests
{
    // Consider something like XMLUnit, but for now the required tests are so simple string
    // comparisons should do. It is not our task to write unit tests verifying an XML writer produce
    // valid XML. :)
    public class SvgWriterTests
    {
        [Fact]
        public async Task WriteComment()
        {
            // Currently comments are ONLY written if the root element is completed. This is not
            // necessarely brilliant, but it works for standard cases so I'll let it slide for now
            StringWriter writer = new StringWriter();
            using (SvgWriter svgWriter = new SvgWriter(new TestRenderSettings(), () => Task.FromResult((TextWriter)writer)))
            {
                await svgWriter.WriteStartElementAsync("root");
                await svgWriter.WriteCommentAsync("comment");
                await svgWriter.WriteEndElementAsync("root");
            }

            string result = writer.ToString();

            Assert.Contains("<!--comment-->", result);
        }

        [Fact]
        public async Task WriteRootElement()
        {
            StringWriter writer = new StringWriter();
            using (SvgWriter svgWriter = new SvgWriter(new TestRenderSettings(), () => Task.FromResult((TextWriter)writer)))
            {
                await svgWriter.WriteStartElementAsync("root");
                await svgWriter.WriteEndElementAsync("root");
            }

            string result = writer.ToString();

            Assert.Contains("<root", result);
            Assert.Contains("http://www.w3.org/2000/svg", result);
            Assert.Contains("</root>", result);
        }

        [Fact]
        public async Task WriteText()
        {
            // Currently comments are ONLY written if the root element is completed. This is not
            // necessarely brilliant, but it works for standard cases so I'll let it slide for now
            StringWriter writer = new StringWriter();
            using (SvgWriter svgWriter = new SvgWriter(new TestRenderSettings(), () => Task.FromResult((TextWriter)writer)))
            {
                await svgWriter.WriteStartElementAsync("root");
                await svgWriter.WriteTextAsync("text");
                await svgWriter.WriteEndElementAsync("root");
            }

            string result = writer.ToString();

            Assert.Contains(">text<", result);
        }

        [Fact]
        public async Task NonInheritedAttributeWrittenTwice()
        {
            // Currently comments are ONLY written if the root element is completed. This is not
            // necessarely brilliant, but it works for standard cases so I'll let it slide for now
            StringWriter writer = new StringWriter();
            using (SvgWriter svgWriter = new SvgWriter(new TestRenderSettings(), () => Task.FromResult((TextWriter)writer)))
            {
                await svgWriter.WriteStartElementAsync("root");
                await svgWriter.WriteNonInheritedAttributeStringAsync("attribute_", "val");
                await svgWriter.WriteStartElementAsync("child");
                await svgWriter.WriteNonInheritedAttributeStringAsync("attribute_", "val");
                await svgWriter.WriteEndElementAsync("child");
                await svgWriter.WriteEndElementAsync("root");
            }

            string result = writer.ToString();
            // Ugly smugly, but will do the job for now :)
            Assert.Equal(result.Count(c => c == '_'), 2);
        }

        [Fact]
        public async Task InheritedAttributeWrittenOnce()
        {
            // Currently comments are ONLY written if the root element is completed. This is not
            // necessarely brilliant, but it works for standard cases so I'll let it slide for now
            StringWriter writer = new StringWriter();
            using (SvgWriter svgWriter = new SvgWriter(new TestRenderSettings(), () => Task.FromResult((TextWriter)writer)))
            {
                await svgWriter.WriteStartElementAsync("root");
                await svgWriter.WriteInheritedAttributeStringAsync("attribute_", "val");
                await svgWriter.WriteStartElementAsync("child");
                await svgWriter.WriteInheritedAttributeStringAsync("attribute_", "val");
                await svgWriter.WriteEndElementAsync("child");
                await svgWriter.WriteEndElementAsync("root");
            }

            string result = writer.ToString();
            // Ugly smugly, but will do the job for now :)
            Assert.Equal(result.Count(c => c == '_'), 1);
        }


        private class TestRenderSettings : RenderSettings
        {
            public override bool PrettyPrint => false;
        }
    }
}