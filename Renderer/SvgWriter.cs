using KiCadDoxer.Renderer.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Xml;

namespace KiCadDoxer.Renderer
{
    public class SvgWriter : SvgFragmentWriter, IDisposable
    {
        private const string SvgNs = "http://www.w3.org/2000/svg";
        private bool isClosed;
        private bool isRootElementWritten;
        private Stack<ElementStackEntry> elementStack = new Stack<ElementStackEntry>();
        private Lazy<Task<XmlWriter>> xmlWriterCreator;
        private Stack<SvgFragmentWriter> fragmentWriterStack = new Stack<SvgFragmentWriter>();

        public SvgWriter()
        {
            XmlWriterSettings xmlWriterSettings = new XmlWriterSettings
            {
                Async = true,
                // TODO: Should not get schematic render settings hardcoded, might be a PCB
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

        public override async Task WriteInheritedAttributeStringAsync(string name, string value)
        {
            await EnsureRootElementWritten();

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

        public override async Task WriteCommentAsync(string comment)
        {
            await EnsureRootElementWritten();

            var xmlWriter = await xmlWriterCreator.Value;
            await xmlWriter.WriteCommentAsync(comment);
        }

        public override async Task WriteEndElementAsync(string name)
        {
            await EnsureRootElementWritten();

            var xmlWriter = await xmlWriterCreator.Value;
            await xmlWriter.WriteEndElementAsync();
            var entry = elementStack.Pop();
            if (entry.Name != name)
            {
                throw new Exception($"Internal SVG render error: Ending element {name} but should end {entry.Name}.");
            }
        }

        public override async Task WriteStartElementAsync(string name)
        {
            await EnsureRootElementWritten();
            var xmlWriter = await xmlWriterCreator.Value;
            var parent = elementStack.PeekOrDefault();
            await xmlWriter.WriteStartElementAsync(null, name, SvgNs);

            elementStack.Push(new ElementStackEntry(parent, name));
        }

        public override async Task WriteTextAsync(string text)
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

            await WriteInheritedAttributeStringAsync("stroke-linecap", "round");
            await WriteInheritedAttributeStringAsync("stroke-linejoin", "round");
            await WriteInheritedAttributeStringAsync("stroke-width", RenderContext.Current.SchematicRenderSettings.DefaultStrokeWidth);
            await WriteInheritedAttributeStringAsync("fill", "none");
            await WriteInheritedAttributeStringAsync("class", "kicad schematics");
        }

    }
}