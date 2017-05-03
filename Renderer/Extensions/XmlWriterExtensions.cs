using System.Threading.Tasks;
using System.Xml;

namespace KiCadDoxer.Renderer.Extensions
{
    internal static class XmlWriterExtensions
    {
        private const string SvgNs = "http://www.w3.org/2000/svg";

        public static Task WriteAttributeStringAsync(this XmlWriter xmlWriter, string localName, string value)
        {
            return xmlWriter.WriteAttributeStringAsync(null, localName, null, value);
        }

        public static Task WriteSvgStartElementAsync(this XmlWriter xmlWriter, string localName)
        {
            return xmlWriter.WriteStartElementAsync(null, localName, SvgNs);
        }
    }
}