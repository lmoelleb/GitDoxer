using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer
{
    public abstract class SchematicRenderSettings
    {
        public virtual bool AddClasses => false;

        public virtual bool AddXlinkToSheets => false;

        public virtual CancellationToken CancellationToken => CancellationToken.None;

        public virtual double DefaultStrokeWidth => 6;

        public virtual HiddenPinRenderMode HiddenPinRenderMode => HiddenPinRenderMode.Hide;

        public virtual bool PrettyPrint => false;

        public virtual bool ShowPinNumbers => true;

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

        public virtual ComponentFieldRenderMode ShowComponentField(int fieldIndex)
        {
            return ComponentFieldRenderMode.Default;
        }
    }
}