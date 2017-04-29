using KiCadDoxer.Renderer;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;
using System.Threading.Tasks;

namespace Renderer.Tests
{
    public class StringLineSource : LineSource
    {
        string value;

        public StringLineSource(TokenizerMode mode, string value)
            : base(CancellationToken.None)
        {
            this.value = value;
            this.Mode = mode;
        }

        protected override Task<TextReader> CreateReader(CancellationToken cancellationToken)
        {
            return Task.FromResult((TextReader)new StringReader(value));
        }
    }
}
