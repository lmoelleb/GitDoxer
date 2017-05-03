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
                    throw new InvalidOperationException("Current must be set before it can be accessed. Are you sure it was not set on a different call context (not an ancestor of this task)");
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

        public virtual CancellationToken CancellationToken => CancellationToken.None;

        internal SchematicRenderSettings SchematicRenderSettings => schematicRenderSettings.Value;

        internal SvgWriter SvgWriter { get; set; }

        public abstract Task<LineSource> CreateLibraryLineSource(string libraryName, CancellationToken cancellationToken);

        public abstract Task<LineSource> CreateLineSource(CancellationToken cancellationToken);

        public abstract Task<TextWriter> CreateOutputWriter(CancellationToken cancellationToken);

        // Could be a property, but I like it looks a bit "symetric" with SetResponseETag
        public virtual string GetRequestETagHeaderValue() => string.Empty;

        public virtual Task<HandleExceptionResult> HandleException(bool canAttemptSvgWrite, Exception ex) => Task.FromResult(HandleExceptionResult.Throw);

        public virtual Task<bool> HandleMatchingETags(CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public virtual void SetResponseEtagHeaderValue(string etag)
        {
        }

        protected abstract SchematicRenderSettings CreateSchematicRenderSettings();
    }
}