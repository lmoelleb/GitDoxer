using KiCadDoxer.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace KiCadDoxer
{
    public class SvgWriter : IDisposable
    {
        private const string SvgNs = "http://www.w3.org/2000/svg";
        private static AsyncLocal<SvgWriter> current = new AsyncLocal<SvgWriter>();
        private Stack<ElementStackEntry> stack = new Stack<ElementStackEntry>();
        private XmlWriter xmlWriter;
        private SvgWriter(SchematicRenderSettings renderSettings, XmlWriter xmlWriter)
        {
            this.xmlWriter = xmlWriter;
            this.RenderSettings = renderSettings;
        }

        public static SvgWriter Current => current.Value;

        public SchematicRenderSettings RenderSettings { get; }

        public static async Task<SvgWriter> CreateAndSetCurrentSvgWriter(SchematicRenderSettings renderSettings)
        {
            XmlWriterSettings xmlWriterSettings = new XmlWriterSettings
            {
                Async = true,
                Indent = renderSettings.PrettyPrint
            };

            XmlWriter xmlWriter = XmlWriter.Create(await renderSettings.CreateOutputWriter(), xmlWriterSettings);

            return new SvgWriter(renderSettings, xmlWriter);
        }
        public static void SetCurrent(SvgWriter svgWriter)
        {
            current.Value = svgWriter;
        }

        public void Dispose()
        {
            xmlWriter.Dispose();
        }

        public Task FlushAsync()
        {
            return xmlWriter.FlushAsync();
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
                await xmlWriter.WriteAttributeStringAsync(null, name, null, value);
            }
        }

        public Task WriteCommentAsync(string comment)
        {
            return xmlWriter.WriteCommentAsync(comment);
        }

        public async Task WriteEndElementAsync(string name)
        {
            await xmlWriter.WriteEndElementAsync();
            var entry = stack.Pop();
            if (entry.Name != name)
            {
                throw new Exception($"Internal SVG render error: Ending element {name} but should end {entry.Name}.");
            }
        }

        public async Task WriteStartElementAsync(string name)
        {
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