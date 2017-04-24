using System.IO;
using System.Threading.Tasks;

namespace KiCadDoxer
{
    public abstract class SchematicRenderSettings
    {
        public bool AddClasses { get; protected set; } = false;

        public bool AddXlinkToSheets { get; protected set; } = false;

        // TODO: Should allow changing this
        public double DefaultStrokeWidth => 6;

        public HiddenPinRenderMode HiddenPinRenderMode { get; set; }

        public bool PrettyPrint { get; protected set; } = false;

        public bool ShowPinNumbers { get; protected set; } = true;

        public abstract Task<LineSource> CreateLibraryLineSource(string libraryName);

        public abstract Task<LineSource> CreateLineSource();
        public abstract Task<TextWriter> CreateOutputWriter();

        public virtual ComponentFieldRenderMode ShowComponentField(int fieldIndex)
        {
            return ComponentFieldRenderMode.Default;
        }
    }
}