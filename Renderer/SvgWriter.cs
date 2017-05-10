using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer
{
    internal class SvgWriter
    {
        private List<WriteOperation> writeOperations = new List<WriteOperation>();

        // Considered optimizations:
        // 1) Object pooling of the write commands
        // 2) Spool write operations to disks once the list gets big - I expect complex PCB's to take
        //    megabytes, and due to single modules containing content accross multiple layers the
        //    entire PCB file has to be processed before the first layer can be written to the output.

        public virtual Task WriteCommentAsync(string comment)
        {
            writeOperations.Add(new CommentWriteOperation(comment));
            return Task.CompletedTask;
        }

        public virtual Task WriteEndElementAsync(string name)
        {
            writeOperations.Add(new EndElementWriteOperation(name));
            return Task.CompletedTask;
        }

        public Task WriteInheritedAttributeStringAsync(string name, int value)
        {
            return WriteInheritedAttributeStringAsync(name, value.ToString(CultureInfo.InvariantCulture));
        }

        public Task WriteInheritedAttributeStringAsync(string name, double value)
        {
            return WriteInheritedAttributeStringAsync(name, value.ToString(CultureInfo.InvariantCulture));
        }

        [Obsolete("This does not check the type, so use a type specific version instead")]
        public Task WriteInheritedAttributeStringAsync(string name, Token token)
        {
            return WriteInheritedAttributeStringAsync(name, (string)token);
        }

        public virtual Task WriteInheritedAttributeStringAsync(string name, string value)
        {
            writeOperations.Add(new InheritedAttributeWriteOperation(name, value));
            return Task.CompletedTask;
        }

        public virtual Task WriteNonInheritedAttributeStringAsync(string name, string value)
        {
            writeOperations.Add(new NonInheritedAttributeWriteOperation(name, value));
            return Task.CompletedTask;
        }

        public Task WriteNonInheritedAttributeStringAsync(string name, int value)
        {
            return WriteNonInheritedAttributeStringAsync(name, value.ToString(CultureInfo.InvariantCulture));
        }

        public virtual Task WriteStartElementAsync(string name)
        {
            writeOperations.Add(new StartElementWriteOperation(name));
            return Task.CompletedTask;
        }

        public virtual Task WriteTextAsync(double x, double y, double angle, string text, TextSettings settings)
        {
            writeOperations.Add(new TextWriteOperation(x, y, angle, text, settings));
            return Task.CompletedTask;
        }

        // Do not convert text to actual SVG before the very last moment (so when it is written to
        // the root writer). This is done for two reasons:
        // 1) It is easier to unit test. The conversion to SVG can be tested at one layer only, then
        //    each text output can be tested on a "higher level" where the text, it's position and
        //    style are tested, not the actual lines drawn.
        // 2) If the writer is holding data in memory awaiting a flush to the Root writer then it
        //    takes a lot less memory than all the rendered polylines makng up the text.
        public virtual Task WriteTextNodeAsync(string text)
        {
            writeOperations.Add(new TextNodeWriteOperation(text));
            return Task.CompletedTask;
        }

        // TODO: Temporarily internal to keep the old SchmaticRenderer happy. Make protected once
        //       SchemaricRenderer is removed.
        protected internal async Task WriteTo(SvgWriter targetWriter)
        {
            foreach (var operation in writeOperations)
            {
                await operation.WriteToFragment(targetWriter);
            }

            writeOperations.Clear();
        }

        private class CommentWriteOperation : WriteOperation
        {
            private string comment;

            public CommentWriteOperation(string comment)
            {
                this.comment = comment;
            }

            protected internal override Task WriteToFragment(SvgWriter fragment)
            {
                return fragment.WriteCommentAsync(comment);
            }
        }

        private class EndElementWriteOperation : WriteOperation
        {
            private string elementName;

            internal EndElementWriteOperation(string elementName)
            {
                this.elementName = elementName;
            }

            protected internal override Task WriteToFragment(SvgWriter fragment)
            {
                return fragment.WriteEndElementAsync(elementName);
            }
        }

        private class InheritedAttributeWriteOperation : WriteOperation
        {
            private string attributeName;
            private string value;

            internal InheritedAttributeWriteOperation(string attributeName, string value)
            {
                this.attributeName = attributeName;
                this.value = value;
            }

            protected internal override Task WriteToFragment(SvgWriter fragment)
            {
                return fragment.WriteInheritedAttributeStringAsync(attributeName, value);
            }
        }

        private class NonInheritedAttributeWriteOperation : WriteOperation
        {
            private string attributeName;
            private string value;

            internal NonInheritedAttributeWriteOperation(string attributeName, string value)
            {
                this.attributeName = attributeName;
                this.value = value;
            }

            protected internal override Task WriteToFragment(SvgWriter fragment)
            {
                return fragment.WriteNonInheritedAttributeStringAsync(attributeName, value);
            }
        }

        private class StartElementWriteOperation : WriteOperation
        {
            private string elementName;

            internal StartElementWriteOperation(string elementName)
            {
                this.elementName = elementName;
            }

            protected internal override Task WriteToFragment(SvgWriter fragment)
            {
                return fragment.WriteStartElementAsync(elementName);
            }
        }

        private class TextNodeWriteOperation : WriteOperation
        {
            private string text;

            public TextNodeWriteOperation(string text)
            {
                this.text = text;
            }

            protected internal override Task WriteToFragment(SvgWriter fragment)
            {
                return fragment.WriteTextNodeAsync(text);
            }
        }

        private class TextWriteOperation : WriteOperation
        {
            private double angle;
            private TextSettings settings;
            private string text;
            private double x;
            private double y;

            public TextWriteOperation(double x, double y, double angle, string text, TextSettings settings)
            {
                this.x = x;
                this.y = y;
                this.angle = angle;
                this.text = text;
                this.settings = settings;
                settings.IsWrittenToOutputAndLocked = true;
            }

            protected internal override Task WriteToFragment(SvgWriter fragment)
            {
                return fragment.WriteTextAsync(x, y, angle, text, settings);
            }
        }

        private abstract class WriteOperation
        {
            protected internal abstract Task WriteToFragment(SvgWriter fragment);
        }
    }
}