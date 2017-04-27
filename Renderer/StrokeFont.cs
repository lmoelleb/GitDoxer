using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer
{
    public static class StrokeFont
    {
        //See http://docs.kicad-pcb.org/doxygen/stroke__font_8cpp_source.html

        private const double InterlinePitchRatio = 1.5;
        private const double OverbarPositionFactor = 1.22;
        private const double StrokeFontScale = 1.0 / 21.0;

        // TODO: Make it retry loading if it fails - now it will cache a failure forever.

        private static Lazy<Task<IList<string>>> fontDataLoader = new Lazy<Task<IList<string>>>(async () =>
          {
              // A lot of characters have the same string value, so make a list of which strings are
              // used where, so they can be assigned the same value yes- probably a waste of time, it
              // is not THAT much memory... but I'll do it anyways - turns out it saves 110K:)
              Dictionary<string, string> stringReferenceNormalizer = new Dictionary<string, string>();

              List<string> result = new List<string>(1024);

              // Space is the first defined character
              for (int i = 0; i < 32; i++)
              {
                  result.Add(string.Empty);
              }

              Regex validLineRegex = new Regex("^\\s*\"([^\"]+)\"\\s*,(\\s*/\\*.*\\*/\\s*)?$");

              // For now, load the stroke font source file from github :)
              using (HttpClient httpClient = new HttpClient())
              {
                  using (Stream stream = await httpClient.GetStreamAsync("https://raw.githubusercontent.com/KiCad/kicad-source-mirror/master/common/newstroke_font.cpp"))
                  {
                      using (StreamReader sr = new StreamReader(stream))
                      {
                          // A "somewhat simplistic view" on the the possible formattings of a cpp
                          // file :)
                          string line;

                          while ((line = await sr.ReadLineAsync()) != null)
                          {
                              Match m = validLineRegex.Match(line);
                              if (m.Success)
                              {
                                  string val = m.Groups[1].Value;
                                  if (val.Contains("\\"))
                                  {
                                      // This is an EXTREMELY naive to decoding - it CAN'T be done
                                      // using search replace supporting all cases - but the only
                                      // character we have encoded is \, so in this narrow case it
                                      // will work.
                                      val = val.Replace(@"\\", @"\");
                                  }

                                  string normalized;
                                  if (stringReferenceNormalizer.TryGetValue(val, out normalized))
                                  {
                                      val = normalized;
                                  }
                                  else
                                  {
                                      stringReferenceNormalizer[val] = val;
                                  }

                                  result.Add(val);
                              }
                          }
                      }
                  }
              }

              return result.AsReadOnly();
          });

        // Yes, it is bloody awful with all these parameters. Introduce a class wich can maintain
        // state like used in KiCad... or bloody ignore it and keep hacking!:)
        public static async Task DrawText(string text, double x, double y, double glyphSize, string stroke, double strokeWidth, bool isBold, bool isItalic, double angle, TextHorizontalJustify horizontalJustify, TextVerticalJustify verticalJustify, string classNames)
        {
            // I have no idea if glyphHeight is the same as size, will try! If not, I can just apply
            // a constant factor
            double lineHeight = glyphSize * InterlinePitchRatio + strokeWidth;

            string[] lines = SplitTextInLines(text).ToArray();

            double offsetX = 0;
            double offsetY = 0;

            // The schematic editor always draws text upwards or rightwards, never downwards or
            // leftwards even when rotated 180 degrees. So turn the text around here if attempting to
            // draw outside these "headings" First normalize the angle from 0-360. Yes, this can
            // probably be done with modula, but then I have to deal with negative - this is easy :)
            while (angle < 0)
            {
                angle += 360;
            }

            while (angle >= 360)
            {
                angle -= 360;
            }
            if (angle > 45 && angle < 225)
            {
                angle += 180;
                if (angle > 360)
                {
                    angle -= 360;
                }

                if (horizontalJustify == TextHorizontalJustify.Left)
                {
                    horizontalJustify = TextHorizontalJustify.Right;
                }
                else if (horizontalJustify == TextHorizontalJustify.Right)
                {
                    horizontalJustify = TextHorizontalJustify.Left;
                }

                if (verticalJustify == TextVerticalJustify.Bottom)
                {
                    verticalJustify = TextVerticalJustify.Top;
                }
                else if (verticalJustify == TextVerticalJustify.Top)
                {
                    verticalJustify = TextVerticalJustify.Bottom;
                }
            }

            if (horizontalJustify != TextHorizontalJustify.Left)
            {
                foreach (var line in lines)
                {
                    offsetX = Math.Min(offsetX, -await GetSingleLineTextWidth(line, glyphSize));
                }

                if (horizontalJustify == TextHorizontalJustify.Center)
                {
                    offsetX /= 2;
                }

                offsetX = Math.Round(offsetX);
            }

            if (verticalJustify != TextVerticalJustify.Bottom)
            {
                offsetY = lineHeight * lines.Length - (lineHeight - (glyphSize + strokeWidth));
                if (verticalJustify == TextVerticalJustify.Center)
                {
                    offsetY /= 2;
                }

                offsetY = Math.Round(offsetY);
            }

            var svgWriter = SvgWriter.Current;

            await svgWriter.WriteStartElementAsync("g");
            List<string> transforms = new List<string>();

            if (angle != 0)
            {
                transforms.Add($"rotate({angle.ToString(CultureInfo.InvariantCulture)},{x.ToString(CultureInfo.InvariantCulture)},{y.ToString(CultureInfo.InvariantCulture)})");
            }

            if (offsetX != 0 || offsetY != 0)
            {
                transforms.Add($"translate({offsetX.ToString(CultureInfo.InvariantCulture)},{offsetY.ToString(CultureInfo.InvariantCulture)})");
            }

            if (transforms.Any())
            {
                await svgWriter.WriteAttributeStringAsync("transform", string.Join(" ", transforms));
            }

            classNames = (classNames + " glyphs").Trim();
            await svgWriter.WriteAttributeStringAsync("class", classNames);
            await svgWriter.WriteAttributeStringAsync("stroke", stroke);
            await svgWriter.WriteAttributeStringAsync("stroke-width", strokeWidth);

            y -= (lines.Length - 1) * lineHeight;
            foreach (var line in lines)
            {
                await DrawSingleLineText(line, x, Math.Round(y), glyphSize, strokeWidth);
                y += lineHeight;
            }

            await svgWriter.WriteEndElementAsync("g");
        }

        public static Task EnsureFontIsLoaded()
        {
            return fontDataLoader.Value;
        }

        private static IEnumerable<(char character, bool overbar)> DecodeString(string text)
        {
            bool includeNextTilde = false;
            bool overbar = false;
            for (int i = 0; i < text.Length; i++)
            {
                // Yes, this escaping is not that clever. If it see the text ~~~ it is not clear if
                // it should render a tilde, then switch to overline or if it should switch to
                // overline, then render a tilde. I think I managed to copy the behaviour of KiCad,
                // not sure - did not check :)
                char c = text[i];
                if (c == '~' && !includeNextTilde)
                {
                    char next = i < text.Length - 1 ? text[i + 1] : (char)0;
                    if (next == '~')
                    {
                        includeNextTilde = true;
                    }
                    else
                    {
                        overbar = !overbar;
                    }

                    continue;
                }

                includeNextTilde = false;
                yield return (c, overbar);
            }
        }

        private static async Task DrawSingleLineText(string text, double x, double y, double glyphSize, double lineWidth)
        {
            var fontData = await fontDataLoader.Value;

            var svgWriter = SvgWriter.Current;

            await svgWriter.WriteCommentAsync(text);
            double? overbarStartPosition = null;
            double? overbarEndPosition = null;

            int overbarY = (int)(y - (glyphSize * OverbarPositionFactor + lineWidth));

            Func<Task> writeOverbar = async () =>
            {
                if (overbarEndPosition == null || overbarStartPosition == null)
                {
                    return;
                }

                await svgWriter.WriteStartElementAsync("line");
                await svgWriter.WriteAttributeStringAsync("class", "overbar");
                await svgWriter.WriteAttributeStringAsync("x1", ((int)overbarStartPosition).ToString(CultureInfo.InvariantCulture));
                await svgWriter.WriteAttributeStringAsync("y1", overbarY.ToString(CultureInfo.InvariantCulture));
                await svgWriter.WriteAttributeStringAsync("x2", ((int)overbarEndPosition).ToString(CultureInfo.InvariantCulture));
                await svgWriter.WriteAttributeStringAsync("y2", overbarY.ToString(CultureInfo.InvariantCulture));
                await svgWriter.WriteEndElementAsync("line");
            };

            foreach (var characterAndFormatting in DecodeString(text))
            {
                char c = characterAndFormatting.character;
                var glyphSizeInfo = await GetSizeInfo(c);

                bool overbar = characterAndFormatting.overbar;

                if (!overbar && overbarEndPosition.HasValue)
                {
                    await writeOverbar();
                }

                if (overbar && !overbarStartPosition.HasValue)
                {
                    overbarStartPosition = x;
                }

                foreach (var polyLinePoints in GetPolylinePoints(c, x, y, glyphSize))
                {
                    await svgWriter.WriteStartElementAsync("polyline");
                    await svgWriter.WriteAttributeStringAsync("points", polyLinePoints);
                    await svgWriter.WriteEndElementAsync("polyline");
                }
                x += glyphSizeInfo.Width * glyphSize;

                if (overbar)
                {
                    overbarEndPosition = x;
                }
            }

            if (overbarEndPosition.HasValue)
            {
                await writeOverbar();
            }
        }

        private static IEnumerable<string> GetPolylinePoints(char c, double glyphX, double glyphY, double glyphSize)
        {
            if (!fontDataLoader.IsValueCreated)
            {
                throw new InvalidOperationException("fontDataLoader must be awaited before calling this method");
            }

            var fontData = fontDataLoader.Value.Result;
            if (c < ' ' | c >= fontData.Count)
            {
                // Maybe I should warn someone.... oh well, I will hate myself in the future, but
                // this is easy :)
                yield break;
            }

            string glyphDef = fontData[c];

            // This could be reused when rendering a text, but then - it is a server, it has time to
            // garbage collect between calls :)
            StringBuilder current = new StringBuilder();

            double glyphStartX = (glyphDef[0] - 'R') * StrokeFontScale;
            double glyphEndX = (glyphDef[1] - 'R') * StrokeFontScale;

            // The first two bytes define the size of the glyph, so they can be ignored
            for (int i = 2; i < glyphDef.Length; i += 2)
            {
                if (glyphDef[i] == ' ' && glyphDef[i + 1] == 'R')
                {
                    if (current.Length > 0)
                    {
                        yield return current.ToString();
                        current.Length = 0;
                    }
                }
                else
                {
                    double x = (glyphDef[i] - 'R') * StrokeFontScale - glyphStartX;
                    const double FontOffset = -10;
                    double y = (glyphDef[i + 1] - 'R' + FontOffset) * StrokeFontScale;
                    x *= glyphSize;
                    y *= glyphSize;
                    x += glyphX;
                    y += glyphY;

                    if (current.Length > 0)
                    {
                        current.Append(' ');
                    }
                    current.Append(((int)x).ToString(CultureInfo.InvariantCulture));
                    current.Append(',');
                    current.Append(((int)y).ToString(CultureInfo.InvariantCulture));
                }
            }

            if (current.Length > 0)
            {
                yield return current.ToString();
            }
        }

        private static async Task<double> GetSingleLineTextWidth(string text, double glyphSize)
        {
            double result = 0;
            foreach (var c in DecodeString(text))
            {
                result += (await GetSizeInfo(c.character)).Width;
            }

            return result * glyphSize;
        }

        private static async Task<(double StartX, double Width)> GetSizeInfo(char c)
        {
            var fontData = await fontDataLoader.Value;
            if (c < ' ' | c >= fontData.Count)
            {
                // Maybe I should warn someone.... oh well, I will hate myself in the future, but
                // this is easy :)
                return (0, 0);
            }

            var glyphDef = fontData[c];

            double glyphStartX = (glyphDef[0] - 'R') * StrokeFontScale;
            double glyphEndX = (glyphDef[1] - 'R') * StrokeFontScale;
            return (glyphStartX, (glyphEndX - glyphStartX));
        }

        private static IEnumerable<string> SplitTextInLines(string text)
        {
            if (!text.Contains("\\n"))
            {
                yield return text;
                yield break;
            }

            // Must fight the urge to name anything with Builder in it "Bob"
            StringBuilder builder = new StringBuilder();

            bool lastWasBackslash = false;
            foreach (char c in text)
            {
                if (c == '\\' && !lastWasBackslash)
                {
                    lastWasBackslash = true;
                    continue;
                }

                if (c == 'n' && lastWasBackslash)
                {
                    yield return builder.ToString();
                    builder.Clear();
                }
                else
                {
                    builder.Append(c);
                }

                lastWasBackslash = false;
            }

            // Yields empty lines as well to allow trailing newline to change layout!
            yield return builder.ToString();
        }
    }
}