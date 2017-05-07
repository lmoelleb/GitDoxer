using KiCadDoxer.Renderer.Exceptions;
using KiCadDoxer.Renderer.Schematic;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer
{
    public abstract class RenderContext
    {
        private static AsyncLocal<RenderContext> current = new AsyncLocal<RenderContext>();
        private Task<LineSource> cacheLibraryLineSourceTask;
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

        // Only virtual for the UnitTest so it can bypass the create method. Maybe worth refactoring!
        internal virtual LineSource LineSource { get; private set; }

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

        public async Task Render()
        {
            // Consider passing in the linesource as a parameter - but that is a refactoring for later.
            using (LineSource = await CreateLineSource(CancellationToken))
            {
                bool setUrl = false;
                if (string.IsNullOrEmpty(LineSource.Url))
                {
                    setUrl = true;
                    LineSource.Url = "Main KiCad file"; // Not ideal, but better than not even knowing if it is in a library or what.
                }

                // The KiCad team is planning (or maybe already working on) EESchema using
                // S-Expressions like pcbnew. Next to this, old pcb files use a format similar to
                // what EESchema use at the time I wrote this comment. But for now, assume legacy
                // format for EESchema, and SExpression for PCB new.
                Token token = await LineSource.Read("EESchema", TokenType.ExpressionOpen);
                if (token.Type == TokenType.ExpressionOpen)
                {
                    token = await LineSource.Read("kicad_pcb");
                }

                using (SvgWriter = new SvgWriter(SchematicRenderSettings, () => CreateOutputWriter(CancellationToken)))
                {
                    try
                    {
                        switch ((string)token)
                        {
                            case "EESchema":
                                if (setUrl)
                                {
                                    LineSource.Url = "KiCad Schematic File";
                                }
                                await SchematicRoot.Render(this);
                                break;

                            case "kicad_pcb":
                                if (setUrl)
                                {
                                    LineSource.Url = "KiCad PCB File";
                                }
                                throw new KiCadFileFormatException(token, "Sorry, PCB files are not yet supported.");
                        }

                        if (LineSource.Mode == TokenizerMode.SExpresionKiCad)
                        {
                            await LineSource.Read(TokenType.ExpressionClose);
                        }

                        await LineSource.Read(TokenType.EndOfFile);
                    }
                    catch (Exception ex)
                    {
                        HandleExceptionResult handleExceptionResult = HandleExceptionResult.Throw;
                        try
                        {
                            bool canAttemptSvgWrite = SvgWriter.IsRootElementWritten && !SvgWriter.IsClosed;
                            handleExceptionResult = await HandleException(canAttemptSvgWrite, ex);
                            if (handleExceptionResult.HasFlag(HandleExceptionResult.WriteToSvg) && canAttemptSvgWrite)
                            {
                                await SvgWriter.WriteStartElementAsync("text");
                                await SvgWriter.WriteInheritedAttributeStringAsync("x", "0");
                                await SvgWriter.WriteInheritedAttributeStringAsync("y", "100");
                                await SvgWriter.WriteInheritedAttributeStringAsync("stroke", "rgb(255,0,0");
                                await SvgWriter.WriteInheritedAttributeStringAsync("fill", "rgb(255,0,0");
                                await SvgWriter.WriteInheritedAttributeStringAsync("font-size", "100");
                                await SvgWriter.WriteTextAsync(ex.Message);
                                await SvgWriter.WriteEndElementAsync("text");
                            }
                        }
                        catch
                        {
                            // TODO: Log this exception to Application Insights - it has no where
                            //       else to go as I want to rethrow the original exception.
                            handleExceptionResult |= HandleExceptionResult.Throw;
                        }

                        if (handleExceptionResult.HasFlag(HandleExceptionResult.Throw))
                        {
                            throw;
                        }
                    }
                    finally
                    {
                        if (cacheLibraryLineSourceTask != null)
                        {
                            (await cacheLibraryLineSourceTask).Dispose();
                        }
                    }
                }
            }
        }

        public virtual void SetResponseEtagHeaderValue(string etag)
        {
        }

        protected abstract SchematicRenderSettings CreateSchematicRenderSettings();
    }
}