using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer
{
    // This class is somewhat agnostic to where it is called from - so it can be used without an http context
    // Warning: This has grown somewhat large - it is very linier and very simple to follow, but
    //          still large. so consider breaking it into multiple classes.
    public class SchematicRenderer
    {
        private const int TxtMargin = 4;

        // Using the file date instead of version number so I do not need a build system messing with
        // the assembly version.
        private static readonly string assemblyFileDate = GetAssemblyFileDateString();

        private static readonly Regex etagRegex = new Regex("^\\s*(?<weak>W\\/)?\"(?<etag>.+)\"\\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private Task<LineSource> cacheLibraryLineSourceTask;
        private List<ComponentPlacement> componentPlacements = new List<ComponentPlacement>();
        private bool isInitialized = false;
        private HashSet<string> knownMultiUnitComponent = new HashSet<string>();
        private LineSource lineSource;
        private HashSet<(int, int)> noConnectPositions = new HashSet<(int, int)>();
        private HashSet<(int, int)> wirePositions = new HashSet<(int, int)>();

        private SvgWriter SvgWriter => SvgWriter.Current;

        public async Task HandleSchematic(SchematicRenderSettings renderSettings)
        {
            var cancellationToken = renderSettings.CancellationToken;
            using (lineSource = await renderSettings.CreateLineSource(cancellationToken))
            {
                lineSource.Mode = TokenizerMode.EeSchema;
                if (string.IsNullOrEmpty(lineSource.Url))
                {
                    lineSource.Url = "KiCad Schematic (.SCH)"; // Not ideal, but better than not even knowing if it is in a library or what.
                }

                using (var writer = new SvgWriter(renderSettings))
                {
                    try
                    {
                        Token firstToken;
                        ComponentPlacement currentComponentPlacement = null;

                        var currentComponentFields = new List<IList<Token>>();

                        // It is not possible to write the component reference right away, as it
                        // needs a letter appended if it is a multiunit component
                        // - something we will not know before reading the component library. So we
                        // must render those later :(

                        var componentReferenceFieldsToRenderAfterLibraryLoad = new List<(ComponentPlacement Placement, IList<Token> Tokens)>();

                        // TODO: Clean up this royal mess - some helper class or two would probably
                        //       help reducing the size of this
                        bool skipToNextLine = false;
                        while (true)
                        {
                            await lineSource.SkipEmptyLines();
                            firstToken = await lineSource.Read(TokenType.Atom, TokenType.EndOfFile);
                            cancellationToken.ThrowIfCancellationRequested();

                            if (currentComponentPlacement != null)
                            {
                                // TODO: Stop feeding these into a list and deal with them one by one
                                List<Token> tokens = new List<Token>();
                                tokens.Add(firstToken);
                                tokens.AddRange(await lineSource.ReadAllTokensUntilEndOfLine());

                                switch ((string)firstToken)
                                {
                                    case "L":
                                        currentComponentPlacement.Name = tokens[1];
                                        currentComponentPlacement.Reference = tokens[2];
                                        break;

                                    case "U":
                                        currentComponentPlacement.UnitIndex = tokens[1];
                                        currentComponentPlacement.ConvertIndex = tokens[2];
                                        break;

                                    case "P":
                                        currentComponentPlacement.PositionX = tokens[1];
                                        currentComponentPlacement.PositionY = tokens[2];
                                        break;

                                    case "F":

                                        // Always try to get rid of the field right away so we do not
                                        // need to keep it in memory if possible (yes, I know I know
                                        // - unecessary optimization that makes the code complex -
                                        // deal with it!
                                        bool knownMultiUnit;
                                        if (currentComponentPlacement.UnitIndex > 1)
                                        {
                                            knownMultiUnitComponent.Add(currentComponentPlacement.Name);
                                            knownMultiUnit = true;
                                        }
                                        else
                                        {
                                            knownMultiUnit = knownMultiUnitComponent.Contains(currentComponentPlacement.Name);
                                        }

                                        // Look the other way, this is going to be ugly :)
                                        if (!knownMultiUnit && tokens[1] == "0")
                                        {
                                            // This is the reference field, for example U304A. We do
                                            // not know at this moment if we need to add a unit
                                            // identifier - we need to defer rendering of the field.

                                            componentReferenceFieldsToRenderAfterLibraryLoad.Add((currentComponentPlacement, tokens));
                                        }
                                        else
                                        {
                                            currentComponentFields.Add(tokens);
                                        }
                                        break;

                                    case "1":
                                    case "0":
                                    case "-1":
                                        if (tokens.Count == 4)
                                        {
                                            // Assume rotation matrix, might need something more
                                            // precise in the future :)
                                            currentComponentPlacement.OrientationMatrix = new int[4];
                                            for (int i = 0; i < 4; i++)
                                            {
                                                string token = tokens[i];
                                                currentComponentPlacement.OrientationMatrix[i] = tokens[i];
                                            }
                                        }
                                        break;

                                    case "$EndComp":
                                        foreach (var field in currentComponentFields)
                                        {
                                            // Must be done after matrix has been read - and that is
                                            // the last line :)
                                            await HandleComponentField(field, currentComponentPlacement);
                                        }
                                        currentComponentFields.Clear();
                                        currentComponentPlacement = null;
                                        break;
                                }
                            }
                            else if (firstToken.Type == TokenType.EndOfFile)
                            {
                                break;
                            }
                            else if (((string)firstToken).StartsWith("LIBS:"))
                            {
                                await HandleLibraryReference(firstToken);
                            }
                            else if (firstToken == "$Comp")
                            {
                                currentComponentPlacement = new ComponentPlacement();
                                componentPlacements.Add(currentComponentPlacement);
                            }
                            else if (firstToken == "$Descr")
                            {
                                if (!await InitializeRendering())
                                {
                                    return; // Done - NotModified returned to the caller!
                                }
                                await HandleDescription();
                            }
                            else if (firstToken == "Wire")
                            {
                                if (!await InitializeRendering())
                                {
                                    return; // Done - NotModified returned to the caller!
                                }
                                await HandleWire();
                            }
                            else if (firstToken == "Connection")
                            {
                                if (!await InitializeRendering())
                                {
                                    return; // Done - NotModified returned to the caller!
                                }
                                await HandleConnection();
                            }
                            else if (firstToken == "NoConn")
                            {
                                if (!await InitializeRendering())
                                {
                                    return; // Done - NotModified returned to the caller!
                                }
                                await HandleNoConnection();
                            }
                            else if (firstToken == "Text")
                            {
                                if (!await InitializeRendering())
                                {
                                    return; // Done - NotModified returned to the caller!
                                }
                                await HandleText();
                                skipToNextLine = false;
                            }
                            else if (firstToken == "$Sheet")
                            {
                                if (!await InitializeRendering())
                                {
                                    return; // Done - NotModified returned to the caller!
                                }
                                await HandleSheet();
                            }

                            if (skipToNextLine)
                            {
                                await lineSource.SkipUntilAfterLineBreak();
                                await lineSource.SkipEmptyLines();
                            }
                            skipToNextLine = true;
                        }

                        if (cacheLibraryLineSourceTask != null && componentPlacements.Any())
                        {
                            await HandleComponentLibrary(await cacheLibraryLineSourceTask);
                            foreach (var deferedComponentReferenceField in componentReferenceFieldsToRenderAfterLibraryLoad)
                            {
                                await HandleComponentField(deferedComponentReferenceField.Tokens, deferedComponentReferenceField.Placement);
                            }
                        }

                        await SvgWriter.WriteEndElementAsync("svg");
                        await SvgWriter.FlushAsync();
                    }
                    catch (Exception ex)
                    {
                        if (!await SvgWriter.Current.RenderSettings.HandleException(ex))
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

        private static string GetAssemblyFileDateString()
        {
            string location = typeof(SchematicRenderer).GetTypeInfo().Assembly.Location;

            if (string.IsNullOrEmpty(location))
            {
                return null;
            }

            // Hmm, no async call... OK, since we cache it I'll et them get away with it and won't
            // branch the .NET core source
            return File.GetLastWriteTimeUtc(location).ToString("yyyyMMddHHmmssffff", CultureInfo.InvariantCulture);
        }

        // Lifted from https://github.com/KiCad/kicad-source-mirror/blob/master/common/drawtxt.cpp
        private double ClampTextPenSize(double aPenSize, double aSize, bool aBold)
        {
            double penSize = aPenSize;
            double scale = aBold ? 4.0 : 6.0;
            double maxWidth = Math.Round(Math.Abs(aSize) / scale);

            if (penSize > maxWidth)
            {
                penSize = maxWidth;
            }

            return penSize;
        }

        private async Task<string> CreateETagHeaderValue()
        {
            if (string.IsNullOrEmpty(assemblyFileDate))
            {
                // For unknown reasons we do not have the file date of the renderer - so we can't set
                // etag headers as we would not notice a new deploy of KiCadDoxer
                return null;
            }

            string etagHeader = await lineSource.ETag;
            if (string.IsNullOrEmpty(etagHeader))
            {
                return null;
            }

            // Weak etags are OK for rendering purpos, so simply ignore the header.
            Match m = etagRegex.Match(etagHeader);
            if (!m.Success)
            {
                return null;
            }

            string etag = m.Groups["etag"].Value;

            // Hmm, copy paste modify code reuse... oops:)

            if (cacheLibraryLineSourceTask != null)
            {
                etagHeader = await (await cacheLibraryLineSourceTask).ETag;

                if (string.IsNullOrEmpty(etagHeader))
                {
                    return null;
                }

                m = etagRegex.Match(etagHeader);
                if (!m.Success)
                {
                    return null;
                }

                // The separator is not 100% bullet proof - but it raises the risk of collisions from
                // EXTREMELY unlikely to VERY EXTREMELY unlikely... and makes it easer to see what is
                // going on :)
                etag += @"_\|/_" + m.Groups["etag"].Value;
            }

            return $"\"{etag}_\\|/_{assemblyFileDate}\"";
        }

        private async Task<LineSource> CreateLibraryLineSource(string name)
        {
            var cancellationToken = SvgWriter.Current.RenderSettings.CancellationToken;
            var result = await SvgWriter.RenderSettings.CreateLibraryLineSource(name + ".lib", cancellationToken);
            result.Mode = TokenizerMode.EeSchema;
            if (string.IsNullOrEmpty(result.Url))
            {
                result.Url = $"KiCad Library ({name})";
            }

            return result;
        }

        // lifted from https://github.com/KiCad/kicad-source-mirror/blob/master/eeschema/sch_text.cpp (SCH_HIERLABEL::GetSchematicTextOffset)
        private (double X, double Y) GetSchematicTextOffset(double lineWidth, int orientation)
        {
            double offset = TxtMargin + (lineWidth + SvgWriter.RenderSettings.DefaultStrokeWidth) / 2;

            double x = 0;
            double y = 0;

            switch (orientation)
            {
                case 0: // Horizontal normale (left justified)
                    y = -offset;
                    break;

                case 1: // Vertical up
                    x = -offset;
                    break;

                case 2: // Horizontal inverted (right justified)
                    y = -offset;
                    break;

                case 3: // Vertical down
                    x = -offset;
                    break;
            }

            return (x, y);
        }

        // lifted from https://github.com/KiCad/kicad-source-mirror/blob/master/eeschema/sch_text.cpp (SCH_HIERLABEL::GetSchematicTextOffset)
        private (double X, double Y) GetSchematicTextOffsetHLabel(double lineWidth, double glyphWidth, int orientation)
        {
            double width = Math.Max(lineWidth, SvgWriter.RenderSettings.DefaultStrokeWidth);
            double ii = glyphWidth + TxtMargin + width;

            double x = 0;
            double y = 0;

            switch (orientation)
            {
                case 0: // Horizontal normale
                    x = -ii;
                    break;

                case 1: // Vertical up
                    y = -ii;
                    break;

                case 2: // Horizontal inverted
                    x = ii;
                    break;

                case 3: // Vertical down
                    y = ii;
                    break;
            }

            return (x, y);
        }

        private async Task HandleComponentArc(ComponentPlacement placement, IList<Token> tokens, bool isFilledRenderPass)
        {
            int unit = tokens[6];
            if (unit != 0 && unit != placement.UnitIndex)
            {
                return;
            }

            int convert = tokens[7];
            if (convert != 0 && convert != placement.ConvertIndex)
            {
                return;
            }

            bool isFilled = tokens[9] == "f";
            if (isFilled != isFilledRenderPass)
            {
                return;
            }

            double radius = tokens[3];
            if (radius <= 0)
            {
                return;
            }

            double startAngle = tokens[4] * 0.1;
            double endAngle = tokens[5] * 0.1;
            var placedStart = placement.TransformToPlacementMils(tokens[10], tokens[11]);
            var placedEnd = placement.TransformToPlacementMils(tokens[12], tokens[13]);

            string sweepFlag = "0";
            if (startAngle > endAngle)
            {
                double temp = endAngle;
                endAngle = startAngle;
                startAngle = temp;
                sweepFlag = "1";
            }

            while (endAngle < startAngle)
            {
                endAngle += 360;
            }

            string largeArcFlag = endAngle - startAngle > 180 ? "1" : "0";

            string path = $"M{((int)placedStart.X).ToString(CultureInfo.InvariantCulture)} {((int)placedStart.Y).ToString(CultureInfo.InvariantCulture)} " +
                          $"A{((int)radius).ToString(CultureInfo.InvariantCulture)} {((int)radius).ToString(CultureInfo.InvariantCulture)} " +
                          $"0.0 {largeArcFlag} {sweepFlag} {((int)placedEnd.X).ToString(CultureInfo.InvariantCulture)} {((int)placedEnd.Y).ToString(CultureInfo.InvariantCulture)}";

            await SvgWriter.WriteStartElementAsync("path");
            await SvgWriter.WriteAttributeStringAsync("stroke-width", tokens[8]);
            await SvgWriter.WriteAttributeStringAsync("fill", isFilled ? "rgb(255,255,194)" : "none");
            await SvgWriter.WriteAttributeStringAsync("stroke", "rgb(132,0,0)");
            await SvgWriter.WriteAttributeStringAsync("d", path);
            await SvgWriter.WriteEndElementAsync("path");
        }

        private async Task HandleComponentCircle(ComponentPlacement placement, IList<Token> tokens, bool isFilledRenderPass)
        {
            int unit = tokens[4];
            if (unit != 0 && unit != placement.UnitIndex)
            {
                return;
            }

            int convert = tokens[6];
            if (convert != 0 && convert != placement.ConvertIndex)
            {
                return;
            }

            bool isFilled = tokens[7] == "f";
            if (isFilled != isFilledRenderPass)
            {
                return;
            }

            var pos = placement.TransformToPlacementMils(tokens[1], tokens[2]);
            await SvgWriter.WriteStartElementAsync("circle");
            await SvgWriter.WriteAttributeStringAsync("cx", pos.X);
            await SvgWriter.WriteAttributeStringAsync("cy", pos.Y);
            await SvgWriter.WriteAttributeStringAsync("r", tokens[3]);
            await SvgWriter.WriteAttributeStringAsync("stroke-width", tokens[6]);
            await SvgWriter.WriteAttributeStringAsync("fill", isFilled ? "rgb(255,255,194)" : "none");
            await SvgWriter.WriteAttributeStringAsync("stroke", "rgb(132,0,0)");
            await SvgWriter.WriteEndElementAsync("circle");
        }

        private async Task HandleComponentDrawing(IEnumerable<ComponentPlacement> placements, LineSource libraryLineSource, double textOffset, bool drawPinNumbers, bool drawPinNames)
        {
            placements = placements.ToList();
            if (!placements.Any())
            {
                return;
            }

            // Filled items have to be rendered first. Then everything else - so we can't render in
            // the order we see the lines, so they must be stuffed into a list :(

            var drawingTokens = new List<IList<Token>>();
            IList<Token> loadedTokens;
            const int maxDrawingComplexity = 10000;

            // TODO: Yet another place I should stop bulk loading tokens
            await libraryLineSource.SkipEmptyLines();
            while ((loadedTokens = (await libraryLineSource.ReadAllTokensUntilEndOfLine()).ToList())[0] != "ENDDRAW")
            {
                await libraryLineSource.SkipEmptyLines();
                if (drawingTokens.Count > maxDrawingComplexity)
                {
                    // Protect against wierd stuff just using memory (yes, I should do that other
                    // places as well)
                    await SvgWriter.WriteCommentAsync($"Maximum drawing complexity of {maxDrawingComplexity} elements reached for component {placements.First().Name} at line# {lineSource.LineNumber}");
                    break;
                }

                drawingTokens.Add(loadedTokens);
            }

            if (!drawingTokens.Any())
            {
                return;
            }

            foreach (var placement in placements)
            {
                await SvgWriter.WriteStartElementAsync("g");
                await SvgWriter.WriteAttributeStringAsync("class", $"component {placement.Name.ToLowerInvariant()} {placement.Reference.ToLowerInvariant()}");

                foreach (bool isFilledRenderPass in new[] { true, false })
                {
                    foreach (var tokens in drawingTokens)
                    {
                        switch ((string)tokens[0])
                        {
                            case "S":
                                await HandleComponentRectangle(placement, tokens, isFilledRenderPass);
                                break;

                            case "C":
                                await HandleComponentCircle(placement, tokens, isFilledRenderPass);
                                break;

                            case "P":
                                await HandleComponentPolyline(placement, tokens, isFilledRenderPass);
                                break;

                            case "X":
                                await HandleComponentPin(placement, tokens, textOffset, drawPinNumbers, drawPinNames);
                                break;

                            case "A":
                                await HandleComponentArc(placement, tokens, isFilledRenderPass);
                                break;

                            case "T":
                                if (!isFilledRenderPass)
                                {
                                    await this.SvgWriter.WriteCommentAsync($"Unsupported draw operation {tokens[0]} in library file at line {libraryLineSource.LineNumber}");
                                }
                                break;
                        }
                    }
                }

                await SvgWriter.WriteEndElementAsync("g");
                placement.Rendered = true;
            }
        }

        private async Task HandleComponentField(IList<Token> fieldTokens, ComponentPlacement placement)
        {
            string text = fieldTokens[2];
            if (string.IsNullOrEmpty(text))
            {
                // No string to render, get out of here
                return;
            }

            ComponentFieldRenderMode renderMode = SvgWriter.RenderSettings.ShowComponentField(fieldTokens[1]);

            if (renderMode == ComponentFieldRenderMode.Hide)
            {
                return;
            }

            bool isVisibleField = ((string)fieldTokens[7])[3] != '1';

            if (renderMode == ComponentFieldRenderMode.Default && !isVisibleField)
            {
                // This field is set to not display, so get out of here unless the parameters force rendering
                return;
            }

            // Add a unit reference if needed
            if (fieldTokens[1] == "0" && knownMultiUnitComponent.Contains(placement.Name))
            {
                text = text + (char)(placement.UnitIndex - 1 + 'A');
            }

            // This is just CRAZY - the x/y position seems to be the abselute position on the
            // schematics... as it would have been if the component is not rotated (and ignoring the
            // component library flips the y axis) I have NO - repeat NO - idea what whoever came up
            // with this was smoking. Make it absolute including rotation etc, or relative. Both will
            // do. Not this!

            (double X, double Y) positionInFile = (fieldTokens[4], fieldTokens[5]);
            var position = placement.TransformFieldLocationToPlacementMils(positionInFile.X, positionInFile.Y);

            double size = fieldTokens[6];

            TextHorizontalJustify horizontalJustify;
            switch ((string)fieldTokens[8])
            {
                case "C":
                    horizontalJustify = TextHorizontalJustify.Center;
                    break;

                case "R":
                    horizontalJustify = TextHorizontalJustify.Right;
                    break;

                default:
                    horizontalJustify = TextHorizontalJustify.Left;
                    break;
            }

            TextVerticalJustify verticalJustify;
            string style = fieldTokens[9];
            switch (style[0])
            {
                // Inverting due to inverted y axis in component libraries
                case 'T':
                    verticalJustify = TextVerticalJustify.Bottom;
                    break;

                case 'C':
                    verticalJustify = TextVerticalJustify.Center;
                    break;

                default:
                    verticalJustify = TextVerticalJustify.Top;
                    break;
            }

            bool isItalic = style[1] == 'I';
            bool isBold = style[2] == 'B';

            string stroke = isVisibleField ? "rgb(0, 132, 132)" : "rgb(132,132,132)";

            var angle = fieldTokens[3] == "H" ? 0.0 : 270.0;

            angle += placement.Angle;

            string classNames = $"kicad schematics component {placement.Name.ToLowerInvariant()} {placement.Reference.ToLowerInvariant()} component-field-{fieldTokens[1]}";
            await StrokeFont.DrawText(text, position.X, position.Y, size, stroke, SvgWriter.RenderSettings.DefaultStrokeWidth, isBold, isItalic, angle, horizontalJustify, verticalJustify, classNames);
        }

        private async Task HandleComponentFromLibrary(LineSource libraryLineSource)
        {
            IList<Token> tokens;
            List<string> namesAndAliases = new List<string>();
            int numberOfUnits = 1;
            double textOffset = 50;
            bool drawPinNumbers = true;
            bool drawPinNames = true;
            do
            {
                await libraryLineSource.SkipEmptyLines();
                tokens = (await libraryLineSource.ReadAllTokensUntilEndOfLine()).ToList();
                switch ((string)tokens[0])
                {
                    case "DEF":
                        namesAndAliases.Add(tokens[1]);
                        numberOfUnits = tokens[7];
                        textOffset = tokens[4];
                        drawPinNumbers = tokens[5];
                        drawPinNames = tokens[6];
                        break;

                    case "ALIAS":
                        namesAndAliases.AddRange(tokens.Skip(1).Select(t => (string)t));
                        break;

                    case "DRAW":
                        await HandleComponentDrawing(componentPlacements.Where(cp => !cp.Rendered && namesAndAliases.Contains(cp.Name)), libraryLineSource, textOffset, drawPinNumbers, drawPinNames);
                        break;
                }
            } while (tokens[0] != "ENDDEF");

            if (numberOfUnits > 1)
            {
                foreach (var name in namesAndAliases)
                {
                    knownMultiUnitComponent.Add(name);
                }
            }
        }

        private async Task HandleComponentLibrary(LineSource libraryLineSource)
        {
            var cancellationToken = SvgWriter.Current.RenderSettings.CancellationToken;
            Token token;
            while (true)
            {
                token = await libraryLineSource.Peek();
                if (token.Type == TokenType.EndOfFile)
                {
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (token == "DEF")
                {
                    await HandleComponentFromLibrary(libraryLineSource);
                }
                else
                {
                    await libraryLineSource.SkipUntilAfterLineBreak();
                }
            }
        }

        private async Task HandleComponentPin(ComponentPlacement placement, IList<Token> tokens, double textOffset, bool drawPinNumbers, bool drawPinNames)
        {
            int unit = tokens[9];
            if (unit != 0 && unit != placement.UnitIndex)
            {
                return;
            }

            int convert = tokens[10];
            if (convert != 0 && convert != placement.ConvertIndex)
            {
                return;
            }

            string pinType = tokens.Count > 12 ? tokens[12] : "";

            bool isHidden = pinType == "N";

            if (isHidden && SvgWriter.RenderSettings.HiddenPinRenderMode == HiddenPinRenderMode.Hide)
            {
                return;
            }

            string name = tokens[1];
            if (name == "~")
            {
                name = "";
            }

            double tx = tokens[3];
            double ty = tokens[4];

            // I expect pin numbers are typical... well.. numbers... but you never know, so keep it a string!
            string pinNumber = tokens[2];
            double length = tokens[5];

            // We do all pin layout as a Right pin, then rotate it into position.
            double a, b, c, d;
            double angle;

            switch (((string)tokens[6]).ToUpperInvariant())
            {
                case "D":
                    a = 0;
                    b = -1;
                    c = 1;
                    d = 0;
                    angle = 90;
                    break;

                case "L":
                    a = -1;
                    b = 0;
                    c = 0;
                    d = -1;
                    angle = 180;
                    break;

                case "U":
                    a = 0;
                    b = 1;
                    c = -1;
                    d = 0;
                    angle = 270;
                    break;

                default:
                    a = 1;
                    b = 0;
                    c = 0;
                    d = 1;
                    angle = 0;
                    break;
            }

            Func<double, double, (double X, double Y)> placePin = (x, y) =>
            {
                double newX = x * a + y * c + tx;
                double newY = x * b + y * d + ty;

                return placement.TransformToPlacementMils(newX, newY);
            };

            var pinLineStart = placePin(0, 0);

            if (isHidden && SvgWriter.RenderSettings.HiddenPinRenderMode == HiddenPinRenderMode.ShowIfConnectedToWire)
            {
                // Show if connected, and not named the same as the component - this hide the pin
                // (and more importantly it's name and number) from for example VCC and Ground
                if (placement.Name == name)
                {
                    return;
                }

                var key = ((int)Math.Round(pinLineStart.X), (int)Math.Round(pinLineStart.Y));
                if (noConnectPositions.Contains(key))
                {
                    //Hmm, consider rendering it in this specific situation as well - so it is clear in the schematics what isn't connected...
                    return;
                }

                if (!wirePositions.Contains(key))
                {
                    return;
                }
            }

            bool isClock = pinType.Contains("C");
            bool isLowIn = pinType == "L";
            bool isInvisible = pinType == "N";
            bool isInverted = pinType.Contains("I");
            bool isLowOut = pinType == "V";
            bool isFallingEdge = pinType == "F";
            bool isNonLogic = pinType == "NX";

            List<string> dynamicClasses = new List<string>();
            if (isClock) dynamicClasses.Add("pin-type-clock");
            if (isLowIn) dynamicClasses.Add("pin-type-low-in");
            if (isInvisible) dynamicClasses.Add("pin-type-invisible");
            if (isInverted) dynamicClasses.Add("pin-type-inverted");
            if (isLowOut) dynamicClasses.Add("pin-type-low-out");
            if (isFallingEdge) dynamicClasses.Add("pin-type-falling-edge");
            if (isNonLogic) dynamicClasses.Add("pin-type-non-logic");
            if (!dynamicClasses.Any()) dynamicClasses.Add("pin-type-line");

            // TODO: Add classes for the electric types

            await SvgWriter.WriteStartElementAsync("g");

            // TODO: Write separate classes for Clock, Low, etc - so a pin can be both low and clock
            //       or inverted and clock!
            await SvgWriter.WriteAttributeStringAsync("class", $"pin pin-{name.ToLowerInvariant()} pin-{pinNumber} {string.Join(" ", dynamicClasses)} pin-dir-{tokens[6].ToLowerInvariant()}");

            await SvgWriter.WriteAttributeStringAsync("stroke", pinType == "N" ? "rgb(132,132,132)" : "rgb(132,0,0)");

            // For now, always render "invisible" pins - need to support an option to show/hide them
            string stroke = pinType == "N" ? "rgb(132,132,132)" : "rgb(132,0,0)";
            string pinNameStroke = pinType == "N" ? "rgb(132,132,132)" : "rgb(0,132,132)";

            var pinLineEnd = placePin(length, 0);

            List<ValueTuple<double, double>> polyLine = new List<(double, double)>();

            if (isInverted)
            {
                if (length > 50)
                {
                    pinLineEnd = placePin(length - 50, 0);
                }
                else
                {
                    pinLineEnd = pinLineStart;
                }
                var invertCirclePos = placePin(length - 25, 0);
                await SvgWriter.WriteStartElementAsync("circle");
                await SvgWriter.WriteAttributeStringAsync("cx", invertCirclePos.X);
                await SvgWriter.WriteAttributeStringAsync("cy", invertCirclePos.Y);
                await SvgWriter.WriteAttributeStringAsync("r", "25");
                await SvgWriter.WriteEndElementAsync("circle");
            }

            if (isClock)
            {
                polyLine.Add(placePin(length, 25));
                polyLine.Add(placePin(length + 50, 0));
                polyLine.Add(placePin(length, -25));
            }

            if (isFallingEdge)
            {
                pinLineEnd = placePin(length - 50, 0);
                polyLine.Add(placePin(length, 25));
                polyLine.Add(placePin(length - 50, 0));
                polyLine.Add(placePin(length, -25));
            }

            if (isLowIn)
            {
                // These are a bit tricly as they do not rotate completely, the graphics is always
                // rendered up/left. Notice I did not try them either, so probably needs fixing :)
                polyLine.Add(pinLineEnd);
                if (pinLineEnd.X > pinLineStart.X)
                {
                    polyLine.Add((pinLineEnd.X - 50, pinLineEnd.Y - 50));
                    polyLine.Add((pinLineEnd.X - 50, pinLineEnd.Y));
                }
                else if (pinLineEnd.X < pinLineStart.X)
                {
                    polyLine.Add((pinLineEnd.X + 50, pinLineEnd.Y - 50));
                    polyLine.Add((pinLineEnd.X + 50, pinLineEnd.Y));
                }
                else if (pinLineEnd.Y > pinLineStart.Y)
                {
                    polyLine.Add((pinLineEnd.X - 50, pinLineEnd.Y - 50));
                    polyLine.Add((pinLineEnd.X, pinLineEnd.Y - 50));
                }
                else if (pinLineEnd.Y < pinLineStart.Y)
                {
                    polyLine.Add((pinLineEnd.X - 50, pinLineEnd.Y + 50));
                    polyLine.Add((pinLineEnd.X, pinLineEnd.Y + 50));
                }
            }

            if (isLowOut)
            {
                // These are a bit tricly as they do not rotate completely, the graphics is always
                // rendered up/left. Notice I did not try them either, so probably needs fixing :)
                if (pinLineEnd.X > pinLineStart.X)
                {
                    polyLine.Add((pinLineEnd.X - 50, pinLineEnd.Y));
                    polyLine.Add((pinLineEnd.X - 50, pinLineEnd.Y - 50));
                }
                else if (pinLineEnd.X < pinLineStart.X)
                {
                    polyLine.Add((pinLineEnd.X + 50, pinLineEnd.Y));
                    polyLine.Add((pinLineEnd.X + 50, pinLineEnd.Y - 50));
                }
                else if (pinLineEnd.Y > pinLineStart.Y)
                {
                    polyLine.Add((pinLineEnd.X, pinLineEnd.Y - 50));
                    polyLine.Add((pinLineEnd.X - 50, pinLineEnd.Y - 50));
                }
                else if (pinLineEnd.Y < pinLineStart.Y)
                {
                    polyLine.Add((pinLineEnd.X, pinLineEnd.Y + 50));
                    polyLine.Add((pinLineEnd.X - 50, pinLineEnd.Y + 50));
                }
            }

            if (isNonLogic)
            {
                await SvgWriter.WriteStartElementAsync("line");
                await SvgWriter.WriteAttributeStringAsync("x1", pinLineEnd.X - 25);
                await SvgWriter.WriteAttributeStringAsync("y1", pinLineEnd.Y - 25);
                await SvgWriter.WriteAttributeStringAsync("x2", pinLineEnd.X + 25);
                await SvgWriter.WriteAttributeStringAsync("x2", pinLineEnd.Y + 25);
                await SvgWriter.WriteEndElementAsync("line");
                await SvgWriter.WriteStartElementAsync("line");
                await SvgWriter.WriteAttributeStringAsync("x1", pinLineEnd.X - 25);
                await SvgWriter.WriteAttributeStringAsync("y1", pinLineEnd.Y + 25);
                await SvgWriter.WriteAttributeStringAsync("x2", pinLineEnd.X + 25);
                await SvgWriter.WriteAttributeStringAsync("x2", pinLineEnd.Y - 25);
                await SvgWriter.WriteEndElementAsync("line");
            }

            if (polyLine.Count > 2)
            {
                await SvgWriter.WriteStartElementAsync("polyline");
                await SvgWriter.WriteAttributeStringAsync("points", string.Join(" ", polyLine.Select(p => $"{p.Item1}, {p.Item2}")));
                await SvgWriter.WriteEndElementAsync("polyline");
            }

            if (length > 0 || (pinLineEnd.X != pinLineStart.X && pinLineEnd.Y != pinLineStart.Y))
            {
                await SvgWriter.WriteStartElementAsync("line");

                await SvgWriter.WriteAttributeStringAsync("x1", pinLineStart.X);
                await SvgWriter.WriteAttributeStringAsync("y1", pinLineStart.Y);
                await SvgWriter.WriteAttributeStringAsync("x2", pinLineEnd.X);
                await SvgWriter.WriteAttributeStringAsync("y2", pinLineEnd.Y);

                await SvgWriter.WriteEndElementAsync("line");
            }

            // full angle is the angle of the pin as it is rendered (so including the component rotation)
            double fullAngle = angle + placement.Angle;
            while (fullAngle >= 360)
            {
                fullAngle -= 360;
            }

            bool drawName = !string.IsNullOrEmpty(name) && drawPinNames;
            bool drawPinNumber = !string.IsNullOrEmpty(pinNumber) && drawPinNumbers && SvgWriter.RenderSettings.ShowPinNumbers;
            double numberTextSize = tokens[8];
            double nameTextSize = tokens[7];

            var defaultLineThickness = SvgWriter.RenderSettings.DefaultStrokeWidth;

            if (length > 0)
            {
                if (drawName)
                {
                    var namePos = placePin(textOffset + length, 0);
                    await StrokeFont.DrawText(name, namePos.X, namePos.Y, nameTextSize, pinNameStroke, defaultLineThickness, false, false, fullAngle, TextHorizontalJustify.Left, TextVerticalJustify.Center, "pin-name");
                }

                if (drawPinNumber)
                {
                    double offsetX = length / 2;
                    double offsetY = GetSchematicTextOffset(defaultLineThickness, 0).Y;
                    TextVerticalJustify verticalJustify = TextVerticalJustify.Top;
                    if (fullAngle < 45 || fullAngle >= 225)
                    {
                        // place the number on the right side of the pin upside down (as it is seen
                        // from the end towards the component). This will place it below the pin in
                        // our pin coordinate system, resulting it in rendering above in the final layout
                        offsetY *= -1;
                        verticalJustify = TextVerticalJustify.Bottom;
                    }

                    var pinNumberPosition = placePin(offsetX, offsetY);

                    await StrokeFont.DrawText(pinNumber, pinNumberPosition.X, pinNumberPosition.Y, numberTextSize, stroke, defaultLineThickness, false, false, fullAngle, TextHorizontalJustify.Center, verticalJustify, "pin-number");
                }
            }
            else
            {
                double offsetY = GetSchematicTextOffset(defaultLineThickness, 0).Y;
                TextVerticalJustify verticalJustify = TextVerticalJustify.Top;
                if (fullAngle < 45 || fullAngle >= 225)
                {
                    // place the number on the right side of the pin upside down (as it is seen from
                    // the end towards the component). This will place it below the pin in our pin
                    // coordinate system, resulting it in rendering above in the final layout
                    offsetY *= -1;
                    verticalJustify = TextVerticalJustify.Bottom;
                }

                if (drawName)
                {
                    var namePos = placePin(0, offsetY);

                    // Hmm, this got itself copy pasted - refactor to have a single draw line and set
                    // up the parameters in this if hell instead.
                    await StrokeFont.DrawText(name, namePos.X, namePos.Y, nameTextSize, pinNameStroke, defaultLineThickness, false, false, fullAngle, TextHorizontalJustify.Center, verticalJustify, "pin-name");
                }

                if (drawPinNumber)
                {
                    var pinNumberPosition = placePin(0, -offsetY);
                    verticalJustify = verticalJustify == TextVerticalJustify.Bottom ? TextVerticalJustify.Top : TextVerticalJustify.Bottom;
                    await StrokeFont.DrawText(pinNumber, pinNumberPosition.X, pinNumberPosition.Y, numberTextSize, stroke, defaultLineThickness, false, false, fullAngle, TextHorizontalJustify.Center, verticalJustify, "pin-number");
                }
            }

            await SvgWriter.WriteEndElementAsync("g");
        }

        private async Task HandleComponentPolyline(ComponentPlacement placement, IList<Token> tokens, bool isFilledRenderPath)
        {
            int unit = tokens[2];
            if (unit != 0 && unit != placement.UnitIndex)
            {
                return;
            }

            int convert = tokens[3];
            if (convert != 0 && convert != placement.ConvertIndex)
            {
                return;
            }

            bool isFilled = tokens.Last() == "f";
            if (isFilled != isFilledRenderPath)
            {
                return;
            }

            int numPoints = tokens[1];
            await SvgWriter.WriteStartElementAsync("polyline");

            StringBuilder points = new StringBuilder();
            for (int i = 0; i < numPoints; i++)
            {
                if (i != 0)
                {
                    points.Append(" ");
                }
                var pos = placement.TransformToPlacementMils(tokens[5 + 2 * i], tokens[6 + 2 * i]);
                points.Append(pos.X);
                points.Append(",");
                points.Append(pos.Y);
            }

            await SvgWriter.WriteAttributeStringAsync("points", points.ToString());
            if (tokens[4] != "0" && tokens[4] != "6")
            {
                await SvgWriter.WriteAttributeStringAsync("stroke-width", tokens[4]);
            }
            if (isFilled)
            {
                await SvgWriter.WriteAttributeStringAsync("fill", "rgb(255,255,194)");
            }
            await SvgWriter.WriteAttributeStringAsync("stroke", "rgb(132,0,0)");
            await SvgWriter.WriteEndElementAsync("polyline");
        }

        private async Task HandleComponentRectangle(ComponentPlacement placement, IList<Token> tokens, bool isFilledRenderPass)
        {
            int unit = tokens[5];
            if (unit != 0 && unit != placement.UnitIndex)
            {
                return;
            }

            int convert = tokens[6];
            if (convert != 0 && convert != placement.ConvertIndex)
            {
                return;
            }

            bool isFilled = tokens[8] == "f";
            if (isFilled != isFilledRenderPass)
            {
                return;
            }

            var transformedStart = placement.TransformToPlacementMils(tokens[1], tokens[2]);
            var transformedEnd = placement.TransformToPlacementMils(tokens[3], tokens[4]);

            double startAngle = tokens[3] * .1;
            double endAngle = tokens[4] * .1;

            //

            await SvgWriter.WriteStartElementAsync("rect");
            await SvgWriter.WriteAttributeStringAsync("x", Math.Min(transformedStart.X, transformedEnd.X));
            await SvgWriter.WriteAttributeStringAsync("y", Math.Min(transformedStart.Y, transformedEnd.Y));
            await SvgWriter.WriteAttributeStringAsync("width", Math.Abs(transformedStart.X - transformedEnd.X));
            await SvgWriter.WriteAttributeStringAsync("height", Math.Abs(transformedStart.Y - transformedEnd.Y));
            await SvgWriter.WriteAttributeStringAsync("stroke-width", tokens[7]);
            await SvgWriter.WriteAttributeStringAsync("fill", isFilled ? "rgb(255,255,194)" : "none");
            await SvgWriter.WriteAttributeStringAsync("stroke", "rgb(132,0,0)");
            await SvgWriter.WriteEndElementAsync("rect");
        }

        private async Task HandleConnection()
        {
            await lineSource.Read(); // ~
            await SvgWriter.WriteStartElementAsync("circle");

            int x = await lineSource.Read();
            int y = await lineSource.Read();
            await SvgWriter.WriteAttributeStringAsync("cx", x);
            await SvgWriter.WriteAttributeStringAsync("cy", y);
            await SvgWriter.WriteAttributeStringAsync("r", "18");
            await SvgWriter.WriteAttributeStringAsync("class", "connection");

            await SvgWriter.WriteAttributeStringAsync("fill", "rgb(0,132,0)");
            await SvgWriter.WriteEndElementAsync("circle");

            wirePositions.Add((x, y));
        }

        private async Task HandleDescription()
        {
            // For now, work with my example, can make it more generic in the future
            await lineSource.Read(); // Sheet size
            var width = await lineSource.Read();
            var height = await lineSource.Read();

            Func<double, string> toMM = mils => (mils * 0.0254).ToString(CultureInfo.InvariantCulture) + "mm";

            // These attributes should never be inherited - maybe rename the inherited to
            // specifically say inherited? - so WriteInheritedAttributeStringAsync?
            await SvgWriter.WriteAttributeStringAsync("width", toMM(width));
            await SvgWriter.WriteAttributeStringAsync("height", toMM(height));
            await SvgWriter.WriteAttributeStringAsync("viewBox", $"0 0 {width} {height}");
        }

        private async Task<bool> HandleETagHeaders()
        {
            var cancellationToken = SvgWriter.Current.RenderSettings.CancellationToken;

            // Bad naming or architecture (probably both). Returning true means rendering is no
            // longer needed - we returned NotModified or something similar
            string etagHeaderValue = await CreateETagHeaderValue();

            if (string.IsNullOrEmpty(etagHeaderValue))
            {
                return false;
            }

            bool continueRendering = true;

            cancellationToken.ThrowIfCancellationRequested();

            if (etagHeaderValue == SvgWriter.RenderSettings.GetRequestETagHeaderValue())
            {
                continueRendering = !await SvgWriter.RenderSettings.HandleMatchingETags(cancellationToken);
            }
            else
            {
                SvgWriter.RenderSettings.SetResponseEtagHeaderValue(etagHeaderValue);
            }

            return continueRendering;
        }

        private async Task HandleLibraryReference(Token firstToken)
        {
            var cancellationToken = SvgWriter.Current.RenderSettings.CancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            string remaining = await lineSource.ReadTextWhileNot(TokenType.EndOfFile, TokenType.LineBreak);
            string libLine = ((string)firstToken).Substring(5) + remaining;
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var lib in libLine.Split(','))
            {
                if (lib.EndsWith("-cache"))
                {
                    cacheLibraryLineSourceTask = CreateLibraryLineSource(lib);
                }
            }
        }

        private async Task HandleNoConnection()
        {
            await lineSource.Read(); // ~
            int x = await lineSource.Read();
            int y = await lineSource.Read();
            noConnectPositions.Add((x, y));

            await SvgWriter.WriteStartElementAsync("g");
            await SvgWriter.WriteAttributeStringAsync("class", "no-connection");

            await SvgWriter.WriteAttributeStringAsync("stroke", "rgb(0,0,132)");

            await SvgWriter.WriteStartElementAsync("line");
            await SvgWriter.WriteAttributeStringAsync("x1", x + 25);
            await SvgWriter.WriteAttributeStringAsync("y1", y + 25);
            await SvgWriter.WriteAttributeStringAsync("x2", x - 25);
            await SvgWriter.WriteAttributeStringAsync("y2", y - 25);
            await SvgWriter.WriteEndElementAsync("line");

            await SvgWriter.WriteStartElementAsync("line");
            await SvgWriter.WriteAttributeStringAsync("x1", x - 25);
            await SvgWriter.WriteAttributeStringAsync("y1", y + 25);
            await SvgWriter.WriteAttributeStringAsync("x2", x + 25);
            await SvgWriter.WriteAttributeStringAsync("y2", y - 25);
            await SvgWriter.WriteEndElementAsync("line");

            await SvgWriter.WriteEndElementAsync("g");
        }

        private async Task HandleSheet()
        {
            await SvgWriter.WriteStartElementAsync("g");
            await SvgWriter.WriteAttributeStringAsync("class", "sheet");

            IList<Token> tokens = null;
            int x = 0, y = 0, width = 0, height = 0;
            await lineSource.SkipEmptyLines();

            // TODO: Argh, more bulkloading to remove
            while ((tokens = (await lineSource.ReadAllTokensUntilEndOfLine()).ToList()).FirstOrDefault() != "$EndSheet")
            {
                if (tokens[0] == "S")
                {
                    x = tokens[1];
                    y = tokens[2];
                    width = tokens[3];
                    height = tokens[4];

                    await SvgWriter.WriteStartElementAsync("rect");
                    await SvgWriter.WriteAttributeStringAsync("x", x);
                    await SvgWriter.WriteAttributeStringAsync("y", y);
                    await SvgWriter.WriteAttributeStringAsync("width", width);
                    await SvgWriter.WriteAttributeStringAsync("height", height);
                    await SvgWriter.WriteAttributeStringAsync("stroke", "rgb(132,0,132)");
                    await SvgWriter.WriteEndElementAsync("rect");
                }
                else if (tokens[0] == "F0")
                {
                    await StrokeFont.DrawText("Sheet: " + tokens[1], x, y + GetSchematicTextOffset(SvgWriter.RenderSettings.DefaultStrokeWidth, 0).Y, tokens[2], "rgb(0,132,132)", 0, false, false, 0, TextHorizontalJustify.Left, TextVerticalJustify.Bottom, "sheet-name");
                }
                else if (tokens[0] == "F1")
                {
                    await StrokeFont.DrawText("File: " + tokens[1], x, y + height - GetSchematicTextOffset(SvgWriter.RenderSettings.DefaultStrokeWidth, 0).Y, tokens[2], "rgb(132,132,0)", 0, false, false, 0, TextHorizontalJustify.Left, TextVerticalJustify.Top, "file-name");
                }
                else if (tokens[0][0] == 'F')
                {
                    // token 2 is I for Input or O for Out. No idea why they can't be bidi or three
                    // state... oh well, not my problem.

                    // Notice the shape is "swapped" for correct rendering using the same code as the HLabel
                    PinSheetLabelShape shape;
                    switch ((string)tokens[2])
                    {
                        case "I":
                            shape = PinSheetLabelShape.Output;
                            break;

                        case "O":
                            shape = PinSheetLabelShape.Input;
                            break;

                        default:
                            shape = PinSheetLabelShape.Unspecified;

                            // TODO: Log to application insights
                            await SvgWriter.WriteCommentAsync($"Unknown sheet label form: {tokens[2]} at line {lineSource.LineNumber} in {lineSource.Url}");
                            break;
                    }
                    int orientation;

                    // The docs I am reading do not mention top/bottom, even though it is clearly
                    // possible by rotating a sheet - to be investigated
                    switch ((string)tokens[3])
                    {
                        case "R":
                            orientation = 0;
                            break;

                        case "L":
                            orientation = 2;
                            break;

                        default:

                            // TODO: Log to application insights
                            await SvgWriter.WriteCommentAsync($"Unknown sheet label orientation: {tokens[3]} at line {lineSource.LineNumber} in {lineSource.Url}");
                            orientation = 0;
                            break;
                    }

                    await HandleText(TextType.HLabel, tokens[1], tokens[4], tokens[5], orientation, tokens[6], shape);
                }

                await lineSource.SkipEmptyLines();
            }

            await SvgWriter.WriteEndElementAsync("g");
        }

        private async Task HandleText()
        {
            // Text and label fields are NASTY, with the number of spaces between tokens being
            // significent in the original source code. Currently I ignore this, as the docs show all
            // the supported text types having the same actual fields...

            var tokens = (await lineSource.ReadAllTokensUntilEndOfLine()).ToList();
            await lineSource.SkipUntilAfterLineBreak();
            string text = await lineSource.ReadTextWhileNot(TokenType.LineBreak, TokenType.EndOfFile);

            var type = tokens[0].ToEnumOrDefault(TextType.Unknown);

            if (type == TextType.Unknown)
            {
                // TODO: Log to application insights!
                await SvgWriter.WriteCommentAsync($"WARNING: Unsupported text type {tokens[0]} at line {tokens[0].LineNumber} in {lineSource.Url}");
                type = TextType.Notes;
            }

            PinSheetLabelShape? shape = null;
            string shapeKey = tokens[5];
            if (!string.IsNullOrEmpty(shapeKey) && shapeKey != "~")
            {
                shape = tokens[5].ToEnumOrDefault(PinSheetLabelShape.Unspecified);
            }

            await HandleText(type, text, tokens[1], tokens[2], tokens[3], tokens[4], shape);
        }

        private async Task HandleText(TextType type, string text, double x, double y, int orientation, double size, string shapeKey)
        {
            PinSheetLabelShape? shape = null;

            if (Enum.TryParse(shapeKey, out PinSheetLabelShape parsedShape))
            {
                shape = parsedShape;
            }

            await HandleText(type, text, x, y, orientation, size, shape);
        }

        private async Task HandleText(TextType type, string text, double x, double y, int orientation, double size, PinSheetLabelShape? shape)
        {
            // Text and label fields are NASTY, with the number of spaces between tokens being
            // significent. Currently I ignore this, expecting to know which values are present based
            // on the text type... but this might have to change.

            // Consider refactoring to a class hierarchy like the KiCad source has it (though they
            // still stuff it in one file...)

            double strokeWidth = SvgWriter.RenderSettings.DefaultStrokeWidth;
            bool isBold = false;
            string stroke = "rgb(192, 64, 64)"; // Hopefully pink enough I will notice it has not been set :)
            double angle = 0;
            TextHorizontalJustify horizontalJustify = TextHorizontalJustify.Left;
            TextVerticalJustify verticalJustify = TextVerticalJustify.Bottom;

            (double X, double Y) textOffset = GetSchematicTextOffset(strokeWidth, orientation);

            switch (type)
            {
                case TextType.Notes:
                    stroke = "rgb(0,0,132)";
                    break;

                case TextType.Label:
                    stroke = "rgb(0,0,0)";
                    break;

                case TextType.HLabel:

                    // TODO: Token[7] can contain "Italic" - I wrote in a comment, but I forgot to
                    //       write where I found this information - probably the source code of KiCad!
                    stroke = "rgb(132,132,0)";
                    textOffset = GetSchematicTextOffsetHLabel(strokeWidth, size, orientation);
                    verticalJustify = TextVerticalJustify.Center;
                    horizontalJustify = TextHorizontalJustify.Right;
                    break;

                default:
                    await SvgWriter.WriteCommentAsync($"WARNING: Unsupported text type {type} at line {lineSource.LineNumber - 1} - rendered with incorrect settings");
                    break;
            }

            strokeWidth = ClampTextPenSize(strokeWidth, size, isBold);

            // This needs a lot more code... This is just an attempt to put something on the screen
            await SvgWriter.WriteStartElementAsync("g");
            await SvgWriter.WriteAttributeStringAsync("class", $"text {type.ToString().ToLowerInvariant()}");

            bool swapVertical = false;
            switch (orientation)
            {
                case 0:
                    angle = 0;
                    break;

                case 1:
                    angle = 270;
                    break;

                case 2:
                    angle = 180;
                    swapVertical = true;
                    break;

                case 3:
                    swapVertical = true;
                    angle = 90;
                    break;
            }

            // TODO: Is this really needed, or is there a problem in DrawText's handling of vertical
            //       alignment? I thought it would do any swapping needed.
            if (swapVertical && verticalJustify != TextVerticalJustify.Center)
            {
                verticalJustify = verticalJustify == TextVerticalJustify.Bottom ? TextVerticalJustify.Top : TextVerticalJustify.Bottom;
            }

            await StrokeFont.DrawText(text, x + textOffset.X, y + textOffset.Y, size, stroke, strokeWidth, false, false, angle, horizontalJustify, verticalJustify, string.Empty);
            if (shape != null)
            {
                int halfSize = (int)size / 2;

                int[] template = TemplateShape[(int)shape][orientation];
                StringBuilder points = new StringBuilder();

                // First number is the length - I do not need that in C#, but kept it in as it is in
                // the original source, and I do not want to mess with the source arrays
                for (int i = 1; i < template.Length; i += 2)
                {
                    if (i > 1)
                    {
                        points.Append(" ");
                    }
                    points.Append((template[i] * halfSize + x).ToString(CultureInfo.InvariantCulture));
                    points.Append(",");
                    points.Append((template[i + 1] * halfSize + y).ToString(CultureInfo.InvariantCulture));
                }

                await SvgWriter.WriteStartElementAsync("polyline");
                await SvgWriter.WriteAttributeStringAsync("class", $"shape");

                await SvgWriter.WriteAttributeStringAsync("stroke", stroke);
                await SvgWriter.WriteAttributeStringAsync("points", points.ToString());
                await SvgWriter.WriteEndElementAsync("polyline");
            }

            await SvgWriter.WriteEndElementAsync("g");
        }

        private async Task HandleWire()
        {
            IEnumerable<Token> type = await lineSource.ReadAllTokensUntilEndOfLine(); //  Wire Line or Bus Line (initial Wire already consumed)
            await lineSource.SkipUntilAfterLineBreak();

            // TODO: Argh, more reading all tokens
            IList<Token> lineDef = (await lineSource.ReadAllTokensUntilEndOfLine()).ToList();
            await SvgWriter.WriteStartElementAsync("line");

            await SvgWriter.WriteAttributeStringAsync("x1", lineDef[0]);
            await SvgWriter.WriteAttributeStringAsync("y1", lineDef[1]);
            await SvgWriter.WriteAttributeStringAsync("x2", lineDef[2]);
            await SvgWriter.WriteAttributeStringAsync("y2", lineDef[3]);

            await SvgWriter.WriteAttributeStringAsync("class", string.Join(" ", type.Select(l => l.ToLowerInvariant())));

            await SvgWriter.WriteAttributeStringAsync("stroke", "rgb(0,132,0)");
            await SvgWriter.WriteEndElementAsync("line");

            wirePositions.Add((lineDef[0], lineDef[1]));
            wirePositions.Add((lineDef[2], lineDef[3]));
        }

        private async Task<bool> InitializeRendering()
        {
            if (isInitialized)
            {
                return true;
            }

            isInitialized = true;

            // Ensure the font is available - a few schematics might not need it, but that would be
            // few so not worth the improved error messages to wait for them.
            await StrokeFont.EnsureFontIsLoaded();

            if (cacheLibraryLineSourceTask != null)
            {
                // Ensure any exceptions are thrown before we start rendering so error results can
                // still be generated
                await (await cacheLibraryLineSourceTask).Peek();
            }

            return await HandleETagHeaders();
        }

        #region LabelTemplates

        // From: https://github.com/KiCad/kicad-source-mirror/blob/master/eeschema/sch_text.cpp
        /* Coding polygons for global symbol graphic shapes.
         *  the first parml is the number of corners
         *  others are the corners coordinates in reduced units
         *  the real coordinate is the reduced coordinate * text half size
         */

        private static int[] Template3STATE_BOTTOM = { 5, 0, 0, -1, 1, 0, 2, 1, 1, 0, 0 };
        private static int[] Template3STATE_HI = { 5, 0, 0, 1, -1, 2, 0, 1, 1, 0, 0 };
        private static int[] Template3STATE_HN = { 5, 0, 0, -1, -1, -2, 0, -1, 1, 0, 0 };
        private static int[] Template3STATE_UP = { 5, 0, 0, -1, -1, 0, -2, 1, -1, 0, 0 };
        private static int[] TemplateBIDI_BOTTOM = { 5, 0, 0, -1, 1, 0, 2, 1, 1, 0, 0 };
        private static int[] TemplateBIDI_HI = { 5, 0, 0, 1, -1, 2, 0, 1, 1, 0, 0 };
        private static int[] TemplateBIDI_HN = { 5, 0, 0, -1, -1, -2, 0, -1, 1, 0, 0 };
        private static int[] TemplateBIDI_UP = { 5, 0, 0, -1, -1, 0, -2, 1, -1, 0, 0 };
        private static int[] TemplateIN_BOTTOM = { 6, 0, 0, 1, 1, 1, 2, -1, 2, -1, 1, 0, 0 };
        private static int[] TemplateIN_HI = { 6, 0, 0, 1, 1, 2, 1, 2, -1, 1, -1, 0, 0 };
        private static int[] TemplateIN_HN = { 6, 0, 0, -1, -1, -2, -1, -2, 1, -1, 1, 0, 0 };
        private static int[] TemplateIN_UP = { 6, 0, 0, 1, -1, 1, -2, -1, -2, -1, -1, 0, 0 };
        private static int[] TemplateOUT_BOTTOM = { 6, 0, 2, 1, 1, 1, 0, -1, 0, -1, 1, 0, 2 };
        private static int[] TemplateOUT_HI = { 6, 2, 0, 1, -1, 0, -1, 0, 1, 1, 1, 2, 0 };
        private static int[] TemplateOUT_HN = { 6, -2, 0, -1, 1, 0, 1, 0, -1, -1, -1, -2, 0 };
        private static int[] TemplateOUT_UP = { 6, 0, -2, 1, -1, 1, 0, -1, 0, -1, -1, 0, -2 };

        private static int[][][] TemplateShape = new int[][][]{
            new int[][] { TemplateIN_HN,     TemplateIN_UP,     TemplateIN_HI,     TemplateIN_BOTTOM     },
            new int[][] { TemplateOUT_HN,    TemplateOUT_UP,    TemplateOUT_HI,    TemplateOUT_BOTTOM    },
            new int[][] { TemplateBIDI_HN,   TemplateBIDI_UP,   TemplateBIDI_HI,   TemplateBIDI_BOTTOM   },
            new int[][] { Template3STATE_HN, Template3STATE_UP, Template3STATE_HI, Template3STATE_BOTTOM },
            new int[][] { TemplateUNSPC_HN,  TemplateUNSPC_UP,  TemplateUNSPC_HI,  TemplateUNSPC_BOTTOM  }};

        private static int[] TemplateUNSPC_BOTTOM = { 5, 1, 0, 1, 2, -1, 2, -1, 0, 1, 0 };
        private static int[] TemplateUNSPC_HI = { 5, 0, -1, 2, -1, 2, 1, 0, 1, 0, -1 };
        private static int[] TemplateUNSPC_HN = { 5, 0, -1, -2, -1, -2, 1, 0, 1, 0, -1 };
        private static int[] TemplateUNSPC_UP = { 5, 1, 0, 1, -2, -1, -2, -1, 0, 1, 0 };

        private enum PinSheetLabelShape
        {
            Input,
            Output,
            BiDi,
            TriState,
            Unspecified
        }

        #endregion LabelTemplates

        private class ComponentPlacement
        {
            public ComponentPlacement()
            {
            }

            public double Angle
            {
                get
                {
                    // Simply rotate a unit vector and see where it ends up. Then we can also ignore
                    // the inverted y axis fun. :)
                    var transformed = TransformToPlacementMils(1, 0);

                    return Math.Atan2(transformed.Y - PositionY, transformed.X - PositionX) * 180.0 / Math.PI;
                }
            }

            public int ConvertIndex { get; set; } = 1;

            public string Name { get; set; }

            public int[] OrientationMatrix { get; set; }

            public double PositionX { get; set; }

            public double PositionY { get; set; }

            public string Reference { get; set; }

            public bool Rendered { get; set; }

            public int UnitIndex { get; set; } = 1;

            public override string ToString()
            {
                return $"Comp({Reference}:{Name})";
            }

            public (double X, double Y) TransformFieldLocationToPlacementMils(double fieldX, double fieldY)
            {
                // This is just crazy. The fields are stored as absolute positions in the schematics
                // except they ignore the component rotation, and the component library inversing the
                // y axis.

                // So this turns the location into a relative position so I can rotate it.
                var dx = fieldX - PositionX;
                var dy = fieldY - PositionY;

                return TransformToPlacementMils(dx, dy);
            }

            public (double X, double Y) TransformToPlacementMils(double libraryX, double libraryY)
            {
                double newX = libraryX * OrientationMatrix[0] + libraryY * OrientationMatrix[2] + PositionX;
                double newY = libraryX * OrientationMatrix[1] + libraryY * OrientationMatrix[3] + PositionY;
                return (newX, newY);
            }

            public (double X, double Y) TransformToPlacementMils((double fieldX, double fieldY) location)
            {
                return TransformToPlacementMils(location.fieldX, location.fieldY);
            }
        }
    }
}