﻿using KiCadDoxer.Renderer.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace KiCadDoxer.Renderer
{
    public class SvgWriter : SvgFragmentWriter, IDisposable
    {
        private const string SvgNs = "http://www.w3.org/2000/svg";
        private Stack<ElementStackEntry> elementStack = new Stack<ElementStackEntry>();
        private Stack<SvgFragmentWriter> fragmentWriterStack = new Stack<SvgFragmentWriter>();
        private bool isClosed;
        private bool isRootElementStartCompleted = false;
        private bool isRootElementStartWritten = false;
        private Lazy<Task<XmlWriter>> xmlWriterCreator;

        public SvgWriter(RenderSettings renderSettings)
            : this(renderSettings, () => RenderContext.Current.CreateOutputWriter(RenderContext.Current.CancellationToken))
        {
        }

        // To be used by unit test. No, it is NOT pretty
        internal SvgWriter(RenderSettings renderSettings, Func<Task<TextWriter>> textWriterFactory)
        {
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
                throw new Exception($"Internal SVG render error: Ending element {name} but should end {entry.Name}.");
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
                // KiCad use 0 to specify default length... nice....
                value = RenderContext.Current.SchematicRenderSettings.DefaultStrokeWidth.ToString(CultureInfo.InvariantCulture);
            }

            if (name == "class" && !RenderContext.Current.SchematicRenderSettings.AddClasses)
            {
                return;
            }

            if (elementStack.Peek().SetInheritedAttribute(name, value))
            {
                var xmlWriter = await xmlWriterCreator.Value;
                await xmlWriter.WriteAttributeStringAsync(null, name, null, value);
            }
        }

        public override async Task WriteStartElementAsync(string name)
        {
            if (!isRootElementStartWritten)
            {
                isRootElementStartWritten = true;
                await base.WriteStartElementAsync(name);
            }
            else if (!isRootElementStartCompleted)
            {
                isRootElementStartCompleted = true;
                await base.WriteTo(this);
            }

            var xmlWriter = await xmlWriterCreator.Value;
            var parent = elementStack.PeekOrDefault();
            await xmlWriter.WriteStartElementAsync(null, name, SvgNs);

            elementStack.Push(new ElementStackEntry(parent, name));
        }

        public override async Task WriteTextAsync(string text)
        {
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