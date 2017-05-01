using KiCadDoxer.Renderer.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Xml;

namespace KiCadDoxer.Renderer
{
    public class SvgWriter : IDisposable
    {
        private const string SvgNs = "http://www.w3.org/2000/svg";
        private bool isClosed;
        private bool isRootElementWritten;
        private Stack<ElementStackEntry> stack = new Stack<ElementStackEntry>();
        private Lazy<Task<XmlWriter>> xmlWriterCreator;

        public SvgWriter()
        {
            XmlWriterSettings xmlWriterSettings = new XmlWriterSettings
            {
                Async = true,
                Indent = RenderContext.Current.SchematicRenderSettings.PrettyPrint
            };

            this.xmlWriterCreator = new Lazy<Task<XmlWriter>>(async () => XmlWriter.Create(
                await RenderContext.Current.CreateOutputWriter(RenderContext.Current.CancellationToken), xmlWriterSettings)
            );
        }

        public bool IsClosed => isClosed;

        public bool IsRootElementWritten => isRootElementWritten;

        public async Task CloseAsync()
        {
            if (isClosed)
            {
                return;
            }

            isClosed = true;

            if (xmlWriterCreator.IsValueCreated)
            {
                if (isRootElementWritten)
                {
                    await WriteEndElementAsync("svg");
                }
            }
        }

        public void Dispose()
        {
            if (xmlWriterCreator.IsValueCreated)
            {
                //Hmm, is this always safe? Come on microsoft, we need async disposable now!!
                xmlWriterCreator.Value.Result.Dispose();
            }
        }

        public async Task FlushAsync()
        {
            if (xmlWriterCreator.IsValueCreated)
            {
                var xmlWriter = await xmlWriterCreator.Value;
                await xmlWriter.FlushAsync();
            }
        }

        public Task WriteAttributeStringAsync(string name, int value)
        {
            return WriteAttributeStringAsync(name, value.ToString(CultureInfo.InvariantCulture));
        }

        public Task WriteAttributeStringAsync(string name, double value)
        {
            return WriteAttributeStringAsync(name, value.ToString(CultureInfo.InvariantCulture));
        }

        public Task WriteAttributeStringAsync(string name, Token token)
        {
            return WriteAttributeStringAsync(name, (string)token);
        }

        public async Task WriteAttributeStringAsync(string name, string value)
        {
            await EnsureRootElementWritten();

            if (name == "stroke-width" && value == "0")
            {
                // KiCad use 0 to determine default length... nice....
                value = RenderContext.Current.SchematicRenderSettings.DefaultStrokeWidth.ToString(CultureInfo.InvariantCulture);
            }

            if (name == "class" && !RenderContext.Current.SchematicRenderSettings.AddClasses)
            {
                return;
            }

            if (stack.Peek().SetInheritedAttribute(name, value))
            {
                var xmlWriter = await xmlWriterCreator.Value;
                await xmlWriter.WriteAttributeStringAsync(null, name, null, value);
            }
        }

        public async Task WriteCommentAsync(string comment)
        {
            await EnsureRootElementWritten();

            var xmlWriter = await xmlWriterCreator.Value;
            await xmlWriter.WriteCommentAsync(comment);
        }

        public async Task WriteEndElementAsync(string name)
        {
            await EnsureRootElementWritten();

            var xmlWriter = await xmlWriterCreator.Value;
            await xmlWriter.WriteEndElementAsync();
            var entry = stack.Pop();
            if (entry.Name != name)
            {
                throw new Exception($"Internal SVG render error: Ending element {name} but should end {entry.Name}.");
            }
        }

        public async Task WriteStartElementAsync(string name)
        {
            await EnsureRootElementWritten();
            var xmlWriter = await xmlWriterCreator.Value;
            var parent = stack.PeekOrDefault();
            await xmlWriter.WriteStartElementAsync(null, name, SvgNs);

            stack.Push(new ElementStackEntry(parent, name));
        }

        public async Task WriteStringAsync(string text)
        {
            await EnsureRootElementWritten();
            var xmlWriter = await xmlWriterCreator.Value;
            await xmlWriter.WriteStringAsync(text);
        }

        private async Task EnsureRootElementWritten()
        {
            if (isRootElementWritten)
            {
                return;
            }

            isRootElementWritten = true;
            try
            {
                await WriteStartElementAsync("svg");
            }
            catch (Exception)
            {
                isRootElementWritten = false;
                throw;
            }

            await WriteAttributeStringAsync("stroke-linecap", "round");
            await WriteAttributeStringAsync("stroke-linejoin", "round");
            await WriteAttributeStringAsync("stroke-width", RenderContext.Current.SchematicRenderSettings.DefaultStrokeWidth);
            await WriteAttributeStringAsync("fill", "none");
            await WriteAttributeStringAsync("class", "kicad schematics");
        }

        public class ElementStackEntry
        {
            private Dictionary<string, string> attributeValues;
            private ElementStackEntry parent;

            public ElementStackEntry(ElementStackEntry parent, string name)
            {
                this.Name = name;
                this.parent = parent;
            }

            public string Name { get; }

            public string GetInheritedAttribute(string name)
            {
                string result = null;
                if (attributeValues != null && !attributeValues.TryGetValue(name, out result))
                {
                    result = parent?.GetInheritedAttribute(name);
                }

                return result;
            }

            public bool SetInheritedAttribute(string name, string value)
            {
                if (GetInheritedAttribute(name) == value)
                {
                    return false;
                }

                if (attributeValues == null)
                {
                    attributeValues = new Dictionary<string, string>();
                }

                attributeValues[name] = value;

                return true;
            }
        }
    }
}