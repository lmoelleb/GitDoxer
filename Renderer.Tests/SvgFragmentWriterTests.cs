using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Xunit;

namespace KiCadDoxer.Renderer.Tests
{
    public class SvgFragmentWriterTests
    {
        [Fact]
        public async Task WriteComment()
        {
            SvgFragmentWriterTestWriter w = new SvgFragmentWriterTestWriter();
            await w.WriteCommentAsync("comment");
            await w.Assert("C:comment");
        }

        [Fact]
        public async Task WriteEndElement()
        {
            SvgFragmentWriterTestWriter w = new SvgFragmentWriterTestWriter();
            await w.WriteEndElementAsync("element");
            await w.Assert("EE:element");
        }

        [Fact]
        public async Task WriteInheritedAttributeDouble()
        {
            SvgFragmentWriterTestWriter w = new SvgFragmentWriterTestWriter();
            var currentCulture = CultureInfo.CurrentCulture;
            try
            {
                // All the tested code will run synchronious as there are no async streams receiving
                // data, so this culture info should flow! Danish use , as decimal separator so this
                // verify doubles are using the invariant culture.
                CultureInfo.CurrentCulture = new CultureInfo("da-DK");
                await w.WriteInheritedAttributeStringAsync("att", 42.666);
                await w.Assert("IA:att,42.666");

                // If this assert fails the test is broken - the culture does not flow - maybe an
                // await was not executed sync?
                Assert.Equal("da-DK", CultureInfo.CurrentCulture.Name);
            }
            finally
            {
                CultureInfo.CurrentCulture = currentCulture;
            }
        }

        [Fact]
        public async Task WriteInheritedAttributeInt()
        {
            SvgFragmentWriterTestWriter w = new SvgFragmentWriterTestWriter();
            await w.WriteInheritedAttributeStringAsync("att", 42);
            await w.Assert("IA:att,42");
        }

        [Fact]
        public async Task WriteInheritedAttributeString()
        {
            SvgFragmentWriterTestWriter w = new SvgFragmentWriterTestWriter();
            await w.WriteInheritedAttributeStringAsync("att", "val");
            await w.Assert("IA:att,val");
        }

        [Fact]
        public async Task WriteNonInheritedAttributeString()
        {
            SvgFragmentWriterTestWriter w = new SvgFragmentWriterTestWriter();
            await w.WriteNonInheritedAttributeStringAsync("att", "val");
            await w.Assert("NIA:att,val");
        }

        [Fact]
        public async Task WriteStartElement()
        {
            SvgFragmentWriterTestWriter w = new SvgFragmentWriterTestWriter();
            await w.WriteStartElementAsync("element");
            await w.Assert("SE:element");
        }

        [Fact]
        public async Task WriteText()
        {
            SvgFragmentWriterTestWriter w = new SvgFragmentWriterTestWriter();
            await w.WriteTextAsync("text");
            await w.Assert("T:text");
        }

        private class SvgFragmentWriterTestWriter : SvgWriter
        {
            // This class only exists to get to the protected member WriteTo
            public async Task Assert(string expected)
            {
                SvgFragmentWriterTestReceiver receiver = new SvgFragmentWriterTestReceiver();
                await WriteTo(receiver);
                Xunit.Assert.Equal(expected, receiver.ToString());
            }

            private class SvgFragmentWriterTestReceiver : SvgWriter
            {
                private List<string> result = new List<string>();

                public override string ToString()
                {
                    return string.Join("|", result);
                }

                public override Task WriteCommentAsync(string comment)
                {
                    result.Add("C:" + comment);
                    return Task.CompletedTask;
                }

                public override Task WriteEndElementAsync(string name)
                {
                    result.Add("EE:" + name);
                    return Task.CompletedTask;
                }

                public override Task WriteInheritedAttributeStringAsync(string name, string value)
                {
                    result.Add("IA:" + name + "," + value);
                    return Task.CompletedTask;
                }

                public override Task WriteNonInheritedAttributeStringAsync(string name, string value)
                {
                    result.Add("NIA:" + name + "," + value);
                    return Task.CompletedTask;
                }

                public override Task WriteStartElementAsync(string name)
                {
                    result.Add("SE:" + name);
                    return Task.CompletedTask;
                }

                public override Task WriteTextAsync(string text)
                {
                    result.Add("T:" + text);
                    return Task.CompletedTask;
                }
            }
        }
    }
}