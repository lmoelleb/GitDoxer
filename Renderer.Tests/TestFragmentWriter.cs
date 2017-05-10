using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer.Tests
{
    internal class TestFragmentWriter : SvgWriter
    {
        private List<(string name, string value, bool isInherited)> currentAttributes;

        private string currentElementName;

        // For now just maintain an ordered list of elements ignoring nesting, comments etc. Might
        // need to improve that :)
        private List<(string ElementName, IList<(string AttributeName, string AttributeValue, bool IsInherited)> Attributes)> elements = new List<(string, IList<(string, string, bool)>)>();

        public override Task WriteCommentAsync(string comment)
        {
            PushCurrentElement();
            return base.WriteCommentAsync(comment);
        }

        public override Task WriteEndElementAsync(string name)
        {
            PushCurrentElement();
            return base.WriteEndElementAsync(name);
        }

        public override Task WriteInheritedAttributeStringAsync(string name, string value)
        {
            currentAttributes.Add((name, value, true));
            return base.WriteInheritedAttributeStringAsync(name, value);
        }

        public override Task WriteNonInheritedAttributeStringAsync(string name, string value)
        {
            currentAttributes.Add((name, value, false));
            return base.WriteNonInheritedAttributeStringAsync(name, value);
        }

        public override Task WriteStartElementAsync(string name)
        {
            PushCurrentElement();
            currentElementName = name;
            currentAttributes = new List<(string, string, bool)>();
            return base.WriteStartElementAsync(name);
        }

        public override Task WriteTextNodeAsync(string text)
        {
            PushCurrentElement();
            return base.WriteTextNodeAsync(text);
        }

        internal bool ContainsElement(string name, params (string AttributeName, string AttributeValue, bool IsInheritedAttribute)[] attributes)
        {
            return CountElements(name, attributes) > 0;
        }

        internal bool ContainsLine(int x1, int y1, int x2, int y2, params (string AttributeName, string AttributeValue, bool IsInheritedAttribute)[] attributes)
        {
            var option1 = (new(string AttributeName, string AttributeValue, bool IsInheritedAttribute)[]
            {
                ("x1", x1.ToString(CultureInfo.InvariantCulture), false),
                ("y1", y1.ToString(CultureInfo.InvariantCulture), false),
                ("x2", x2.ToString(CultureInfo.InvariantCulture), false),
                ("y2", y2.ToString(CultureInfo.InvariantCulture), false),
            }).Concat(attributes).ToArray();
            var option2 = (new(string AttributeName, string AttributeValue, bool IsInheritedAttribute)[]
            {
                ("x2", x1.ToString(CultureInfo.InvariantCulture), false),
                ("y2", y1.ToString(CultureInfo.InvariantCulture), false),
                ("x1", x2.ToString(CultureInfo.InvariantCulture), false),
                ("y1", y2.ToString(CultureInfo.InvariantCulture), false),
            }).Concat(attributes).ToArray();

            bool containsLine1 = ContainsSingleElement("line", option1);
            bool containsLine2 = ContainsSingleElement("line", option2);

            return containsLine1 || containsLine2;
        }

        internal bool ContainsSingleElement(string name, params (string AttributeName, string AttributeValue, bool IsInheritedAttribute)[] attributes)
        {
            return CountElements(name, attributes) == 1;
        }

        internal int CountElements(string elementName, params (string AttributeName, string AttributeValue, bool IsInheritedAttribute)[] attributes)
        {
            int result = 0;

            // Yes, this is a somewhat naive implementation performacne wise. If unit tests get slow
            // then deal with it. If not, then ignore. For now, ignoring inherited/non-inherited attributes

            var lookup = new HashSet<(string, string, bool)>(attributes.Select(a => (a.AttributeName, a.AttributeValue, a.IsInheritedAttribute)));

            foreach (var element in elements)
            {
                if (element.ElementName != elementName)
                {
                    continue;
                }

                if (element.Attributes.Count != attributes.Length)
                {
                    continue;
                }

                bool found = true;
                foreach (var att in element.Attributes)
                {
                    if (!lookup.Contains((att.AttributeName, att.AttributeValue, att.IsInherited)))
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                {
                    result++;
                }
            }

            return result;
        }

        internal int CountElementsIgnoringAttributes(string elementName)
        {
            return elements.Count(e => e.ElementName == elementName);
        }

        private void PushCurrentElement()
        {
            if (currentElementName != null)
            {
                elements.Add((currentElementName, currentAttributes));
                currentElementName = null;
                currentAttributes = null;
            }
        }
    }
}