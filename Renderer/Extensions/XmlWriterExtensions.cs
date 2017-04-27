using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace KiCadDoxer.Renderer.Extensions
{
    public static class XmlWriterExtensions
    {
        private const string SvgNs = "http://www.w3.org/2000/svg";

        public static Task WriteSvgStartElementAsync(this XmlWriter xmlWriter, string localName)
        {
            return xmlWriter.WriteStartElementAsync(null, localName, SvgNs);
        }

        public static Task WriteAttributeStringAsync(this XmlWriter xmlWriter, string localName, string value)
        {
            return xmlWriter.WriteAttributeStringAsync(null, localName, null, value);
        }
    }
}
