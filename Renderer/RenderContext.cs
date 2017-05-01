using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer
{
    public abstract class RenderContext
    {
        private static AsyncLocal<RenderContext> current = new AsyncLocal<RenderContext>();
        private Lazy<SchematicRenderSettings> schematicRenderSettings;

        public RenderContext()
        {
            schematicRenderSettings = new Lazy<SchematicRenderSettings>(() => CreateSchematicRenderSettings());
        }

        public static RenderContext Current
        {
            get
            {
                if (current.Value == null)
                {
                    throw new InvalidOperationException("Current must be set before it can be accessed. Are you sure it was not set on a different call context (not an ansestor of this task)");
                }

                return current.Value;
            }
            internal set
            {
                if (current.Value != null && current.Value != value)
                {
                    throw new InvalidOperationException("Current can only be set once on this call context");
                }
                current.Value = value;
            }
        }

        protected abstract SchematicRenderSettings CreateSchematicRenderSettings();

        internal SchematicRenderSettings SchematicRenderSettings => schematicRenderSettings.Value;

        public virtual CancellationToken CancellationToken => CancellationToken.None;

        // Considered creating it on demand in here, but I want to manage the disposing of it
        // differently as the context is created by the assembly calling the renderer, while the
        // SvgWriter is created from within this assembly and needs to be disposed as we finish rendering.
        public SvgWriter SvgWriter { get; internal set; }

        public abstract Task<LineSource> CreateLibraryLineSource(string libraryName, CancellationToken cancellationToken);

        public abstract Task<LineSource> CreateLineSource(CancellationToken cancellationToken);

        public abstract Task<TextWriter> CreateOutputWriter(CancellationToken cancellationToken);

        // Could be a property, but I like it looks a bit "symetric" with SetResponseETag
        public virtual string GetRequestETagHeaderValue() => string.Empty;

        public virtual Task<bool> HandleException(Exception ex) => Task.FromResult(false);

        public virtual Task<bool> HandleMatchingETags(CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public virtual void SetResponseEtagHeaderValue(string etag)
        {
        }
    }
}