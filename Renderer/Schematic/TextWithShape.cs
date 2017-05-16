using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer.Schematic
{
    internal abstract class TextWithShape : Text
    {
        protected TextWithShape(RenderContext renderContext) : base(renderContext)
        {
        }

        protected virtual double PointyShapePartWidth => this.TextSettings.Size / 2.0;

        protected virtual double ShapeHeight => this.TextSettings.Size;

        protected virtual double ShapeInnerWidth => 0;

        protected virtual double ShapeWidth => this.TextSettings.Size;

        protected virtual double StaightShapePartWidth => this.TextSettings.Size / 2.0;

        protected override async Task RenderShape(double x, double y, double angle, Shape shape)
        {
            await Writer.WriteStartElementAsync("polyline");
            await Writer.WriteInheritedAttributeStringAsync("stroke", TextSettings.Stroke);
            await Writer.WriteInheritedAttributeStringAsync("stroke-width", TextSettings.StrokeWidth);
            await Writer.WriteNonInheritedAttributeStringAsync("class", "shape");

            int swapX = TextSettings.HorizontalJustify == TextHorizontalJustify.Left ? -1 : 1;

            var radAngle = angle * Math.PI / 180.0;
            var cosRadAngle = Math.Cos(radAngle);
            var sinRadAngle = Math.Sin(radAngle);

            var pointTexts = from p in ShapePoints(shape)
                             let xr = p.X * cosRadAngle - p.Y * sinRadAngle
                             let yr = p.X * sinRadAngle + p.Y * cosRadAngle
                             let xt = xr * swapX + x
                             let yt = yr + y
                             let xint = (int)Math.Round(xt)
                             let yint = (int)Math.Round(yt)
                             select $"{xint.ToString(CultureInfo.InvariantCulture)},{yint.ToString(CultureInfo.InvariantCulture)}";
            await Writer.WriteNonInheritedAttributeStringAsync("points", string.Join(" ", pointTexts));
            await Writer.WriteEndElementAsync("polyline");
        }

        protected IEnumerable<(double X, double Y)> ShapePoints(Shape shape)
        {
            // All is calculated for a "RIGHT" type text (which is a left to right, right aligned
            // thing), then it is rotated into place elsewhere
            bool isLeftPointy = false;
            bool isRightPointy = false;

            switch (shape)
            {
                case Shape.Bidirectional:
                    isLeftPointy = true;
                    isRightPointy = true;
                    break;

                case Shape.Input:
                    isRightPointy = true;
                    break;

                case Shape.Output:
                    isLeftPointy = true;
                    break;
            }

            bool isMinimalWidth = ShapeWidth <= this.TextSettings.Size;
            double slopeWidth = isMinimalWidth ? ShapeWidth / 2 : this.TextSettings.Size / 2;

            double halfHeight = ShapeHeight / 2;
            (double, double) firstPoint;

            // First render the left side from bottom to the top
            if (isLeftPointy)
            {
                firstPoint = (-ShapeWidth + slopeWidth, halfHeight);
                yield return firstPoint;
                yield return (-ShapeWidth, 0);
                yield return (-ShapeWidth + slopeWidth, -halfHeight);
            }
            else
            {
                firstPoint = (-ShapeWidth, halfHeight);
                yield return firstPoint;
                yield return (-ShapeWidth, -halfHeight);
            }

            // Then render the right side from the top to the bottom
            if (isLeftPointy && isRightPointy && isMinimalWidth)
            {
                // In this situation the two ends meet at the top and bottom, so the points besides
                // the center are only needed on one side
                yield return (0, 0);
            }
            else if (isRightPointy)
            {
                yield return (-slopeWidth, -halfHeight);
                yield return (0, 0);
                yield return (-slopeWidth, halfHeight);
            }
            else
            {
                yield return (0, -halfHeight);
                yield return (0, halfHeight);
            }

            // Close the shape
            yield return firstPoint;
        }
    }
}