using KiCadDoxer.Renderer.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace KiCadDoxer.Renderer
{
    public class SvgWriter : IDisposable
    {
        private const string SvgNs = "http://www.w3.org/2000/svg";
        private static AsyncLocal<SvgWriter> current = new AsyncLocal<SvgWriter>();
        private Stack<ElementStackEntry> stack = new Stack<ElementStackEntry>();
        private Lazy<Task<XmlWriter>> xmlWriterCreator;

        public SvgWriter(SchematicRenderSettings renderSettings)
        {
            if (current.Value != null)
            {
                throw new NotSupportedException("The current SvgWriter can't be changed once it is created in a sync call context");
            }

            XmlWriterSettings xmlWriterSettings = new XmlWriterSettings
            {
                Async = true,
                Indent = renderSettings.PrettyPrint
            };

            this.xmlWriterCreator = new Lazy<Task<XmlWriter>>(async () => XmlWriter.Create(await renderSettings.CreateOutputWriter(renderSettings.CancellationToken), xmlWriterSettings));
            this.RenderSettings = renderSettings;
            current.Value = this;
        }

        public static SvgWriter Current => current.Value;

        public SchematicRenderSettings RenderSettings { get; }

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
            if (name == "stroke-width" && value == "0")
            {
                // KiCad use 0 to determine default length... nice....
                value = this.RenderSettings.DefaultStrokeWidth.ToString(CultureInfo.InvariantCulture);
            }

            if (name == "class" && !RenderSettings.AddClasses)
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
            var xmlWriter = await xmlWriterCreator.Value;
            await xmlWriter.WriteCommentAsync(comment);
        }

        public async Task WriteEndElementAsync(string name)
        {
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
            var xmlWriter = await xmlWriterCreator.Value;
            var parent = stack.PeekOrDefault();
            await xmlWriter.WriteStartElementAsync(null, name, SvgNs);

            if (stack.Count == 0 && RenderSettings.AddXlinkToSheets)
            {
                await xmlWriter.WriteAttributeStringAsync("xmlns", "xlink", null, "http://www.w3.org/1999/xlink");
            }

            stack.Push(new ElementStackEntry(parent, name));
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