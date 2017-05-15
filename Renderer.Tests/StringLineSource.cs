using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer.Tests
{
    public class StringLineSource : LineSource
    {
        private string value;

        internal StringLineSource(TokenizerMode mode, string value)
            : base(CancellationToken.None)
        {
            this.value = value;
            this.Mode = mode;
        }

        public static async Task<string> GetUnescapedLineString(string escapedString)
        {
            using (var source = new StringLineSource(TokenizerMode.SExpresionKiCad, escapedString))
            {
                return await source.Read(TokenType.LineOfText);
            }
        }

        public static async Task<string> GetUnescapedString(string escapedString)
        {
            using (var source = new StringLineSource(TokenizerMode.SExpresionKiCad, "\"" + escapedString + "\""))
            {
                return await source.Read();
            }
        }

        protected override Task<TextReader> CreateReader(CancellationToken cancellationToken)
        {
            return Task.FromResult((TextReader)new StringReader(value));
        }
    }
}