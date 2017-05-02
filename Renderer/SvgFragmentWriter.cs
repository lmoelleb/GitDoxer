using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer
{
    public class SvgFragmentWriter
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

        public virtual Task WriteStartElementAsync(string name)
        {
            writeOperations.Add(new StartElementWriteOperation(name));
            return Task.CompletedTask;
        }

        public virtual Task WriteTextAsync(string text)
        {
            writeOperations.Add(new TextWriteOperation(text));
            return Task.CompletedTask;
        }

        protected async Task WriteTo(SvgFragmentWriter targetWriter)
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

            protected internal override Task WriteToFragment(SvgFragmentWriter fragment)
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

            protected internal override Task WriteToFragment(SvgFragmentWriter fragment)
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

            protected internal override Task WriteToFragment(SvgFragmentWriter fragment)
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

            protected internal override Task WriteToFragment(SvgFragmentWriter fragment)
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

            protected internal override Task WriteToFragment(SvgFragmentWriter fragment)
            {
                return fragment.WriteStartElementAsync(elementName);
            }
        }

        private class TextWriteOperation : WriteOperation
        {
            private string text;

            public TextWriteOperation(string text)
            {
                this.text = text;
            }

            protected internal override Task WriteToFragment(SvgFragmentWriter fragment)
            {
                return fragment.WriteTextAsync(text);
            }
        }

        private abstract class WriteOperation
        {
            protected internal abstract Task WriteToFragment(SvgFragmentWriter fragment);
        }
    }
}