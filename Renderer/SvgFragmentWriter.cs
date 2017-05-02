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

        public virtual Task WriteTextAsync(string comment)
        {
            writeOperations.Add(new TextWriteOperation(comment));
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

        protected async Task WriteTo(SvgFragmentWriter targetWriter)
        {
            foreach (var operation in writeOperations)
            {
                await operation.WriteToFragment(targetWriter);
            }
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

        private class TextWriteOperation : WriteOperation
        {
            private string text;

            public TextWriteOperation(string comment)
            {
                this.text = comment;
            }

            protected internal override Task WriteToFragment(SvgFragmentWriter fragment)
            {
                return fragment.WriteCommentAsync(text);
            }
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

        private abstract class WriteOperation
        {
            protected internal abstract Task WriteToFragment(SvgFragmentWriter fragment);
        }
    }
}