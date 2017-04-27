using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer
{
    public abstract class SchematicRenderSettings
    {
        public bool AddClasses { get; protected set; } = false;

        public bool AddXlinkToSheets { get; protected set; } = false;

        public virtual CancellationToken CancellationToken => CancellationToken.None;

        // TODO: Should allow changing this
        public double DefaultStrokeWidth => 6;

        public HiddenPinRenderMode HiddenPinRenderMode { get; set; }

        public bool PrettyPrint { get; protected set; } = false;

        public bool ShowPinNumbers { get; protected set; } = true;

        public abstract Task<LineSource> CreateLibraryLineSource(string libraryName, CancellationToken cancellationToken);

        public abstract Task<LineSource> CreateLineSource(CancellationToken cancellationToken);

        public abstract Task<TextWriter> CreateOutputWriter(CancellationToken cancellationToken);

        // Could be a property, but I like it looks a bit "symetric" with SetResponseETag
        public virtual string GetRequestETagHeaderValue() => string.Empty;

        public virtual Task<bool> HandleMatchingETags(CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public virtual void SetResponseEtagHeaderValue(string etag)
        {
        }

        public virtual ComponentFieldRenderMode ShowComponentField(int fieldIndex)
        {
            return ComponentFieldRenderMode.Default;
        }
    }
}