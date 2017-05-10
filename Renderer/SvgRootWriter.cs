using KiCadDoxer.Renderer.Exceptions;
using KiCadDoxer.Renderer.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace KiCadDoxer.Renderer
{
    internal class SvgRootWriter : SvgWriter, IDisposable
    {
        private const string SvgNs = "http://www.w3.org/2000/svg";
        private Stack<ElementStackEntry> elementStack = new Stack<ElementStackEntry>();
        private Stack<SvgWriter> fragmentWriterStack = new Stack<SvgWriter>();
        private bool isClosed;
        private bool isRootElementStartCompleted = false;
        private bool isRootElementStartWritten = false;
        private RenderSettings renderSettings;
        private Lazy<Task<XmlWriter>> xmlWriterCreator;

        internal SvgRootWriter(RenderSettings renderSettings, Func<Task<TextWriter>> textWriterFactory)
        {
            this.renderSettings = renderSettings;
            XmlWriterSettings xmlWriterSettings = new XmlWriterSettings
            {
                Async = true,
                Indent = renderSettings.PrettyPrint
            };

            this.xmlWriterCreator = new Lazy<Task<XmlWriter>>(async () => XmlWriter.Create(await textWriterFactory(), xmlWriterSettings));
        }

        public bool IsClosed => isClosed;

        public bool IsRootElementWritten => isRootElementStartWritten;

        public async Task CloseAsync()
        {
            if (isClosed)
            {
                return;
            }

            isClosed = true;

            if (xmlWriterCreator.IsValueCreated)
            {
                if (isRootElementStartWritten)
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

        public override async Task WriteCommentAsync(string comment)
        {
            if (!isRootElementStartCompleted)
            {
                await base.WriteCommentAsync(comment);
                return;
            }

            var xmlWriter = await xmlWriterCreator.Value;
            await xmlWriter.WriteCommentAsync(comment);
        }

        public override async Task WriteEndElementAsync(string name)
        {
            if (!isRootElementStartCompleted)
            {
                isRootElementStartCompleted = true;
                await WriteTo(this);
            }

            var xmlWriter = await xmlWriterCreator.Value;
            await xmlWriter.WriteEndElementAsync();
            var entry = elementStack.Pop();
            if (entry.Name != name)
            {
                throw new InternalRenderException($"Ending element {name} but should end {entry.Name}.");
            }

            if (entry.Name == "svg" && elementStack.Count != 0)
            {
                throw new InternalRenderException($"The \"svg\" element must be the last element closed.");
            }

            if (entry.Name != "svg" && elementStack.Count == 0)
            {
                throw new InternalRenderException($"Root element being ended must have the name \"svg\".");
            }
        }

        public override async Task WriteInheritedAttributeStringAsync(string name, string value)
        {
            if (!isRootElementStartCompleted)
            {
                await base.WriteInheritedAttributeStringAsync(name, value);
                return;
            }

            if (name == "stroke-width" && value == "0")
            {
                // KiCad use 0 to specify default length... nice.... I do need a more generic way to
                // deal with this though, so keeping "Current" for now, so the new unit tests will
                // throw on it.
                value = RenderContext.Current.SchematicRenderSettings.DefaultStrokeWidth.ToString(CultureInfo.InvariantCulture);
            }

            if (name == "class" && !renderSettings.AddClasses)
            {
                return;
            }

            if (elementStack.Peek().SetInheritedAttribute(name, value))
            {
                var xmlWriter = await xmlWriterCreator.Value;
                await xmlWriter.WriteAttributeStringAsync(null, name, null, value);
            }
        }

        public override async Task WriteNonInheritedAttributeStringAsync(string name, string value)
        {
            if (!isRootElementStartCompleted)
            {
                await base.WriteInheritedAttributeStringAsync(name, value);
                return;
            }

            if (name == "class" && !renderSettings.AddClasses)
            {
                return;
            }

            // Someone can still choose to inherit eve if this element shouldn't, I do not really care.
            elementStack.Peek().SetInheritedAttribute(name, value);
            var xmlWriter = await xmlWriterCreator.Value;
            await xmlWriter.WriteAttributeStringAsync(null, name, null, value);
        }

        public override async Task WriteStartElementAsync(string name)
        {
            if (!isRootElementStartWritten)
            {
                isRootElementStartWritten = true;
                await base.WriteStartElementAsync(name);
                return;
            }
            else if (!isRootElementStartCompleted)
            {
                isRootElementStartCompleted = true;
                await base.WriteTo(this);
            }

            if (elementStack.Count == 0 && name != "svg")
            {
                throw new InternalRenderException("The root element must have the name \"svg\".");
            }
            var xmlWriter = await xmlWriterCreator.Value;
            var parent = elementStack.PeekOrDefault();
            await xmlWriter.WriteStartElementAsync(null, name, SvgNs);

            elementStack.Push(new ElementStackEntry(parent, name));
        }

        public override Task WriteTextAsync(double x, double y, double angle, string text, TextSettings settings)
        {
            return StrokeFont.DrawText(this, renderSettings, x, y, angle, text, settings);
        }

        public override async Task WriteTextNodeAsync(string text)
        {
            if (!isRootElementStartCompleted)
            {
                await base.WriteTextNodeAsync(text);
                return;
            }

            var xmlWriter = await xmlWriterCreator.Value;
            await xmlWriter.WriteStringAsync(text);
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
                if (attributeValues == null || !attributeValues.TryGetValue(name, out result))
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