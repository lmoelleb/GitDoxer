using System;

namespace KiCadDoxer.Renderer
{
    internal enum TextHorizontalJustify
    {
        Left, Center, Right
    }

    internal static class TextHorizontalJustifyExtensions
    {
        public static TextHorizontalJustify GetInverse(this TextHorizontalJustify justification)
        {
            switch (justification)
            {
                case TextHorizontalJustify.Left:
                    return TextHorizontalJustify.Right;

                case TextHorizontalJustify.Right:
                    return TextHorizontalJustify.Left;

                case TextHorizontalJustify.Center:
                    return TextHorizontalJustify.Center;

                default:
                    throw new ArgumentException("Unknown horizontal text justification: " + justification, nameof(justification));
            }
        }
    }
}