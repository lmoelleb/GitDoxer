using System;

namespace KiCadDoxer.Renderer
{
    internal enum TextVerticalJustify
    {
        Bottom, Top, Center
    }

    internal static class TextVerticalJustifyExtensions
    {
        public static TextVerticalJustify GetInverse(this TextVerticalJustify justification)
        {
            switch (justification)
            {
                case TextVerticalJustify.Bottom:
                    return TextVerticalJustify.Top;

                case TextVerticalJustify.Top:
                    return TextVerticalJustify.Bottom;

                case TextVerticalJustify.Center:
                    return TextVerticalJustify.Center;

                default:
                    throw new ArgumentException("Unknown vertical text justification: " + justification, nameof(justification));
            }
        }
    }
}